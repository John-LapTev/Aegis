using System.Text.Json;
using Aegis.System.Internal;

namespace Aegis.System.Backup;

/// <summary>Запись о бэкапе отключённой задачи планировщика (для возврата = повторного включения).</summary>
public sealed record ScheduledTaskBackupRecord
{
    public required string Id { get; init; }
    public required string TaskPath { get; init; }
    public required string TaskName { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Обратимость отключения лишних задач планировщика (телеметрия/реклама): ПЕРЕД отключением сохраняем
/// запись (задача была включена), а возврат — <c>schtasks /change /tn … /enable</c>. Записи лежат в
/// %LOCALAPPDATA%\Aegis\backups\tasks и показываются в разделе «Бэкапы» (ADR 0002).
/// </summary>
public sealed class ScheduledTaskBackupStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "backups", "tasks");

    /// <summary>Сохранить запись о предстоящем отключении задачи и вернуть id бэкапа (null при ошибке).</summary>
    public string? Backup(string taskPath, string taskName, string description)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var id = Guid.NewGuid().ToString("N");
            var record = new ScheduledTaskBackupRecord
            {
                Id = id,
                TaskPath = taskPath,
                TaskName = taskName,
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

    /// <summary>Удалить запись бэкапа без действий над системой (если отключить задачу не удалось).</summary>
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

    /// <summary>Вернуть задачу (включить) и удалить запись бэкапа. false — id не наш (не найден).</summary>
    public bool Restore(string id)
    {
        var record = Load(id);
        if (record is null)
        {
            return false;
        }

        // Честный откат: при провале включения бросаем ошибку, а не рапортуем успех (запись бэкапа при этом НЕ стираем —
        // можно повторить из «Бэкапов»). Симметрично отключению: если schtasks не берёт (защищённые задачи), пробуем
        // PowerShell Enable-ScheduledTask — как ScheduledTaskDisableFix делает для отключения (аудит 2026-07-04).
        if (!ProcessRunner.RunSync(ProcessRunner.System("schtasks.exe"), $"/change /tn \"{record.TaskPath}\" /enable")
            && !TryPowerShellEnable(record.TaskPath))
        {
            throw new InvalidOperationException("Не удалось включить задачу обратно.");
        }

        Discard(id);
        return true;
    }

    /// <summary>Запасной путь включения задачи через PowerShell (как у отключения) — разбирает путь на папку+имя.</summary>
    private static bool TryPowerShellEnable(string taskPath)
    {
        var lastSlash = taskPath.LastIndexOf('\\');
        var folder = lastSlash > 0 ? taskPath[..(lastSlash + 1)] : "\\";
        var name = lastSlash >= 0 ? taskPath[(lastSlash + 1)..] : taskPath;
        return ProcessRunner.RunSync(
            ProcessRunner.System(@"WindowsPowerShell\v1.0\powershell.exe"),
            $"-NoProfile -NonInteractive -Command \"Enable-ScheduledTask -TaskPath '{folder}' -TaskName '{name}'\"");
    }

    public IReadOnlyList<ScheduledTaskBackupRecord> List()
    {
        var result = new List<ScheduledTaskBackupRecord>();
        if (!Directory.Exists(Folder))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                var record = JsonSerializer.Deserialize<ScheduledTaskBackupRecord>(File.ReadAllText(file));
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

    private static ScheduledTaskBackupRecord? Load(string id)
    {
        var path = Path.Combine(Folder, id + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScheduledTaskBackupRecord>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
