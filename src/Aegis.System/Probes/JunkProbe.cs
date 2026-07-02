using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник мусора: измеряет размер известных мест мусора Windows и кэшей браузеров/программ
/// (как делают CCleaner / «Очистка диска»). Только читает.
/// </summary>
public sealed class JunkProbe : IJunkProbe
{
    public Task<IReadOnlyList<JunkCandidate>> FindAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new List<JunkCandidate>();

        // Временные файлы и системный мусор.
        Add(candidates, Path.GetTempPath(), JunkCategory.TempFiles, cancellationToken);
        Add(candidates, Expand(@"%WINDIR%\Temp"), JunkCategory.TempFiles, cancellationToken);
        Add(candidates, Expand(@"%LOCALAPPDATA%\CrashDumps"), JunkCategory.TempFiles, cancellationToken);
        Add(candidates, Expand(@"%WINDIR%\SoftwareDistribution\Download"), JunkCategory.WindowsUpdateCache, cancellationToken);
        Add(candidates, Expand(@"%LOCALAPPDATA%\Microsoft\Windows\Explorer"), JunkCategory.ThumbnailCache, cancellationToken);
        Add(candidates, Expand(@"%LOCALAPPDATA%\Microsoft\Windows\INetCache"), JunkCategory.Cache, cancellationToken);

        // Кэши браузеров (Chrome/Edge/Brave/Yandex/Vivaldi/Firefox/Opera) по ВСЕМ профилям измеряет
        // AppCacheProbe (каталог AppCacheCatalog). Здесь их НЕ считаем — иначе один и тот же кэш попал бы
        // в сумму «Всего в разделе» дважды. JunkProbe отвечает только за системный мусор Windows выше.

        return Task.FromResult<IReadOnlyList<JunkCandidate>>(candidates);
    }

    private static string Expand(string path) => Environment.ExpandEnvironmentVariables(path);

    private static void Add(List<JunkCandidate> list, string path, JunkCategory category, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            var size = DirectorySize(path, cancellationToken);
            if (size > 0)
            {
                list.Add(new JunkCandidate { Path = path, SizeBytes = size, Category = category });
            }
        }
        catch (Exception)
        {
            // Папка недоступна — пропускаем (best-effort).
        }
    }

    private static long DirectorySize(string path, CancellationToken cancellationToken)
    {
        long total = 0;

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        // DirectoryInfo.EnumerateFiles даёт FileInfo с УЖЕ известным размером (из данных перечисления),
        // поэтому .Length НЕ делает лишний syscall на каждый файл — вдвое меньше обращений к диску.
        foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                total += file.Length;
            }
            catch (Exception)
            {
                // Файл исчез/занят — пропускаем.
            }
        }

        return total;
    }
}
