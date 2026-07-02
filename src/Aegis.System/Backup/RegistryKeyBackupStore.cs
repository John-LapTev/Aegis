using System.Text.Json;
using Aegis.System.Internal;

namespace Aegis.System.Backup;

/// <summary>Запись о бэкапе ветки реестра (экспорт в .reg) — для восстановления удалённого ключа.</summary>
public sealed record RegistryKeyBackupRecord
{
    public required string Id { get; init; }
    public required string KeyPath { get; init; }
    public required string FilePath { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Бэкап целых веток реестра через штатный <c>reg.exe export</c> (в %LOCALAPPDATA%\Aegis\backups\regkeys)
/// и восстановление через <c>reg.exe import</c>. Нужно для обратимого удаления ключей (осиротевшие записи).
/// </summary>
public sealed class RegistryKeyBackupStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "backups", "regkeys");

    /// <summary>Экспортировать ветку (например, <c>HKLM\SOFTWARE\…\Uninstall\X</c>) и вернуть id бэкапа.</summary>
    public string? Backup(string fullKeyPath, string description)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var id = Guid.NewGuid().ToString("N");
            var file = Path.Combine(Folder, id + ".reg");

            if (!ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"export \"{fullKeyPath}\" \"{file}\" /y"))
            {
                return null;
            }

            var record = new RegistryKeyBackupRecord
            {
                Id = id,
                KeyPath = fullKeyPath,
                FilePath = file,
                Description = description,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            File.WriteAllText(Path.Combine(Folder, id + ".json"), JsonSerializer.Serialize(record));
            return id;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Вернуть ветку реестра из экспорта. false — id не наш; исключение — id наш, но файл экспорта потерян.</summary>
    public bool Restore(string id)
    {
        var record = Load(id);
        if (record is null)
        {
            return false; // не наш бэкап
        }

        if (!File.Exists(record.FilePath))
        {
            throw new InvalidOperationException("Файл бэкапа реестра не найден — вернуть ветку не получилось.");
        }

        ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"import \"{record.FilePath}\"");
        return true;
    }

    public IReadOnlyList<RegistryKeyBackupRecord> List()
    {
        var result = new List<RegistryKeyBackupRecord>();
        if (!Directory.Exists(Folder))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                var record = JsonSerializer.Deserialize<RegistryKeyBackupRecord>(File.ReadAllText(file));
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

    private static RegistryKeyBackupRecord? Load(string id)
    {
        var path = Path.Combine(Folder, id + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RegistryKeyBackupRecord>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
