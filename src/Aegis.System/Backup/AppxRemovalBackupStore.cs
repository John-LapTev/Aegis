using System.Text.Json;
using Aegis.System.Internal;

namespace Aegis.System.Backup;

/// <summary>Запись об удалённом UWP-приложении (для возврата).</summary>
public sealed record AppxRemovalBackupRecord
{
    public required string Id { get; init; }
    public required string PackageFullName { get; init; }
    public required string AppName { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Обратимость удаления встроенных UWP-приложений: ПЕРЕД удалением сохраняем запись (имя пакета),
/// возврат — best-effort повторная регистрация пакета (<c>Add-AppxPackage -register</c>), если файлы ещё на
/// месте; иначе приложение возвращается из Microsoft Store вручную (об этом сказано в UI). Записи — в
/// %LOCALAPPDATA%\Aegis\backups\appx, показываются в разделе «Бэкапы» (ADR 0002).
/// </summary>
public sealed class AppxRemovalBackupStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "backups", "appx");

    /// <summary>Сохранить запись о предстоящем удалении приложения. Возвращает id бэкапа (null при ошибке).</summary>
    public string? Backup(string packageFullName, string appName, string description)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var id = Guid.NewGuid().ToString("N");
            var record = new AppxRemovalBackupRecord
            {
                Id = id,
                PackageFullName = packageFullName,
                AppName = appName,
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

    /// <summary>Удалить запись бэкапа без действий (если удалить приложение не удалось).</summary>
    public void Discard(string id)
    {
        try
        {
            var path = Path.Combine(Folder, id + ".json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Файл недоступен — не критично.
        }
    }

    /// <summary>Вернуть приложение (best-effort регистрация) и удалить запись. false — id не наш (не найден).</summary>
    public bool Restore(string id)
    {
        var record = Load(id);
        if (record is null)
        {
            return false;
        }

        // Файлы пакета обычно остаются под WindowsApps — пробуем повторно зарегистрировать.
        var manifest = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps", record.PackageFullName, "AppxManifest.xml");

        // Честный откат: при провале регистрации бросаем ошибку и НЕ снимаем запись (иначе показывали бы «Возвращено»,
        // а приложение не вернулось, и повторить из «Бэкапов» было бы нельзя — аудит 2026-07-04). Файлы пакета могли
        // быть удалены — тогда вернуть можно только вручную из Microsoft Store.
        if (!ProcessRunner.RunSync(
                "powershell.exe",
                $"-NoProfile -NonInteractive -Command \"Add-AppxPackage -DisableDevelopmentMode -Register '{manifest}'\"",
                30000))
        {
            throw new InvalidOperationException("Не удалось вернуть приложение — переустанови его из Microsoft Store.");
        }

        Discard(id);
        return true;
    }

    public IReadOnlyList<AppxRemovalBackupRecord> List()
    {
        var result = new List<AppxRemovalBackupRecord>();
        if (!Directory.Exists(Folder))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                var record = JsonSerializer.Deserialize<AppxRemovalBackupRecord>(File.ReadAllText(file));
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

    private static AppxRemovalBackupRecord? Load(string id)
    {
        var path = Path.Combine(Folder, id + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AppxRemovalBackupRecord>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
