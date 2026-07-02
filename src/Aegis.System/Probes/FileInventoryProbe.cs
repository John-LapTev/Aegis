using System.Security.Cryptography;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник пользовательских файлов (Загрузки/Документы/Рабочий стол/Видео/Музыка/Картинки).
/// Дубли ищет «воронкой» (как czkawka, реализовано clean-room): группировка по размеру → схлопывание
/// «жёстких ссылок» → предхэш начала+конца → полный хэш только настоящим кандидатам. Так быстрее и честнее
/// (одинаковый размер ≠ дубль, жёсткие ссылки не считаются «лишними копиями»). Только читает.
/// </summary>
public sealed class FileInventoryProbe : IFileInventoryProbe
{
    private const long CandidateThreshold = 2L * 1024 * 1024;        // 2 МБ — порог кандидата в дубли
    private const long MaxHashSize = 4L * 1024 * 1024 * 1024;        // файлы крупнее не хэшируем (долго)
    private const int MaxFiles = 1500;                               // ограничение, чтобы скан не затягивался
    private const int PrehashBytes = 4 * 1024;                       // предхэш: начало + конец по 4 КБ

    public Task<IReadOnlyList<FileEntry>> FindAsync(CancellationToken cancellationToken = default)
    {
        var collected = new List<(string Path, long Size)>();
        foreach (var folder in UserFolders())
        {
            Collect(folder, collected, cancellationToken);
            if (collected.Count >= MaxFiles)
            {
                break;
            }
        }

        var result = new List<FileEntry>();
        foreach (var sizeGroup in collected.GroupBy(static c => c.Size))
        {
            HashGroup(sizeGroup.Key, sizeGroup.ToList(), result, cancellationToken);
        }

        return Task.FromResult<IReadOnlyList<FileEntry>>(result);
    }

    // Воронка дублей внутри одной размерной группы: жёсткие ссылки → предхэш → полный хэш.
    private static void HashGroup(long size, List<(string Path, long Size)> group, List<FileEntry> result, CancellationToken cancellationToken)
    {
        // Уникальный размер или слишком большой файл — точно не дубль (или дорого хэшировать): без хэша.
        if (group.Count < 2 || size <= 0 || size > MaxHashSize)
        {
            AddPlain(group, result);
            return;
        }

        // Схлопнуть жёсткие ссылки: один и тот же файл по разным путям — не «лишняя копия».
        var seenIdentities = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<(string Path, long Size)>();
        foreach (var item in group)
        {
            var identity = FileIdentity.TryGet(item.Path);
            if (identity is not null && !seenIdentities.Add(identity))
            {
                result.Add(Plain(item)); // жёсткая ссылка на уже учтённый файл — не дубль
                continue;
            }

            candidates.Add(item);
        }

        if (candidates.Count < 2)
        {
            AddPlain(candidates, result);
            return;
        }

        // Предхэш (начало+конец): отсеять «похожие по размеру, но разные» без полного чтения.
        foreach (var preGroup in candidates.GroupBy(c => PreHash(c.Path, c.Size)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var members = preGroup.ToList();
            if (preGroup.Key.Length == 0 || members.Count < 2)
            {
                AddPlain(members, result);
                continue;
            }

            // Полный хэш — только тем, кто совпал и по размеру, и по предхэшу.
            foreach (var (path, memberSize) in members)
            {
                result.Add(new FileEntry { Path = path, SizeBytes = memberSize, ContentHash = TryHash(path) });
            }
        }
    }

    private static void AddPlain(IEnumerable<(string Path, long Size)> items, List<FileEntry> result)
    {
        foreach (var item in items)
        {
            result.Add(Plain(item));
        }
    }

    private static FileEntry Plain((string Path, long Size) item) =>
        new() { Path = item.Path, SizeBytes = item.Size, ContentHash = string.Empty };

    // Предхэш: первые и последние PrehashBytes (или весь файл, если он меньше). Ловит файлы, что
    // совпадают в начале, но различаются в конце (архивы/контейнеры) — чего не видит «хэш только начала».
    private static string PreHash(string path, long size)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[PrehashBytes];

            // ReadExactly (а не Read) — чтобы короткое чтение не давало разный предхэш у одинаковых файлов.
            stream.ReadExactly(buffer, 0, buffer.Length);
            hasher.AppendData(buffer, 0, buffer.Length);

            if (size > PrehashBytes * 2)
            {
                stream.Seek(-PrehashBytes, SeekOrigin.End);
                stream.ReadExactly(buffer, 0, buffer.Length);
                hasher.AppendData(buffer, 0, buffer.Length);
            }

            return Convert.ToHexString(hasher.GetHashAndReset());
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> UserFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private static void Collect(string folder, List<(string Path, long Size)> into, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            // FileInfo из DirectoryInfo.EnumerateFiles несёт готовый размер — без лишнего syscall на файл.
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (into.Count >= MaxFiles)
                {
                    return;
                }

                try
                {
                    var size = file.Length;
                    if (size >= CandidateThreshold)
                    {
                        into.Add((file.FullName, size));
                    }
                }
                catch (Exception)
                {
                    // Файл исчез/занят — пропускаем.
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Папка недоступна — пропускаем.
        }
    }

    private static string TryHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
