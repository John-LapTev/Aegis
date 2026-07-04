using System.Text.Json;

namespace Aegis.System.Backup;

/// <summary>Запись о файле, перемещённом в карантин (для восстановления на место).</summary>
public sealed record QuarantineRecord
{
    public required string Id { get; init; }
    public required string OriginalPath { get; init; }
    public required string StoredPath { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Карантин файлов: перемещает файл в %LOCALAPPDATA%\Aegis\quarantine (вместо удаления) и умеет вернуть
/// его на место. Обратимая альтернатива удалению — для автозапуска из папки и (позже) угроз.
/// </summary>
public sealed class QuarantineStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "quarantine");

    public string Quarantine(string filePath, string description)
    {
        Directory.CreateDirectory(Folder);
        var id = Guid.NewGuid().ToString("N");
        var stored = Path.Combine(Folder, $"{id}_{Path.GetFileName(filePath)}");

        // Пишем record ДО перемещения файла: иначе сбой между Move и записью оставил бы файл в карантине БЕЗ записи —
        // осиротевший, не восстановимый через приложение (аудит 2026-07-04). Если Move не удался — убираем запись.
        var record = new QuarantineRecord
        {
            Id = id,
            OriginalPath = filePath,
            StoredPath = stored,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var recordPath = Path.Combine(Folder, id + ".json");
        File.WriteAllText(recordPath, JsonSerializer.Serialize(record));
        try
        {
            File.Move(filePath, stored);
        }
        catch (Exception)
        {
            TryDelete(recordPath); // не оставляем запись без файла; своё исключение не маскируем
            throw;
        }

        return id;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Не удалось убрать запись — не маскируем исходную ошибку перемещения.
        }
    }

    /// <summary>Вернуть файл из карантина на место. false — id не наш; исключение — id наш, но файл потерян.</summary>
    public bool Restore(string id)
    {
        var record = Load(id);
        if (record is null)
        {
            return false; // не наш бэкап
        }

        if (!File.Exists(record.StoredPath))
        {
            throw new InvalidOperationException("Файл из карантина не найден — вернуть на место не получилось.");
        }

        var directory = Path.GetDirectoryName(record.OriginalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Move(record.StoredPath, record.OriginalPath, overwrite: true);
        File.Delete(Path.Combine(Folder, id + ".json"));
        return true;
    }

    public IReadOnlyList<QuarantineRecord> List()
    {
        var result = new List<QuarantineRecord>();
        if (!Directory.Exists(Folder))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                var record = JsonSerializer.Deserialize<QuarantineRecord>(File.ReadAllText(file));
                if (record is not null)
                {
                    result.Add(record);
                }
            }
            catch (Exception)
            {
                // Битая запись — пропускаем.
            }
        }

        return result.OrderByDescending(r => r.CreatedAt).ToList();
    }

    private static QuarantineRecord? Load(string id)
    {
        var path = Path.Combine(Folder, id + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QuarantineRecord>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
