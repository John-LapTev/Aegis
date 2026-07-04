using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Обратимое отключение автозапуска. Для записи реестра (Run) — бэкап значения и его удаление; для
/// ярлыка/файла из папки автозагрузки — перенос в карантин. И то, и другое можно вернуть.
/// </summary>
public sealed class AutostartDisableFix : IFix
{
    private readonly IReadOnlyDictionary<string, string> _data;
    private readonly RegistryBackupStore _registry;
    private readonly QuarantineStore _quarantine;

    public AutostartDisableFix(
        string findingId,
        IReadOnlyDictionary<string, string> data,
        RegistryBackupStore registry,
        QuarantineStore quarantine)
    {
        FindingId = findingId;
        _data = data;
        _registry = registry;
        _quarantine = quarantine;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Autostart;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(_data.GetValueOrDefault(FindingDataKeys.Kind) switch
            {
                "autostart-run" => DisableRunEntry(),
                "autostart-startup" => DisableStartupFile(),
                _ => FixOutcome.Failed("Неизвестный тип автозапуска."),
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось отключить автозапуск: " + ex.Message));
        }
    }

    private FixOutcome DisableRunEntry()
    {
        var hive = RegistryHiveNames.ToHive(_data["hive"]);
        var subKey = _data["subkey"];
        var name = _data["name"];

        var backupId = _registry.Backup(hive, subKey, name, "Отключение автозапуска: " + name);

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);

        return FixOutcome.Ok(backupId);
    }

    private FixOutcome DisableStartupFile()
    {
        var file = _data["file"];
        if (!File.Exists(file))
        {
            return FixOutcome.OkWithoutBackup();
        }

        var id = _quarantine.Quarantine(file, "Автозапуск из папки: " + Path.GetFileName(file));
        return FixOutcome.Ok(id);
    }
}
