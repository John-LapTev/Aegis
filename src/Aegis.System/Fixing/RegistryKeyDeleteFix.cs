using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Обратимое удаление ветки реестра (например, осиротевшей записи об удалённой программе): сначала
/// экспорт ветки в .reg (<see cref="RegistryKeyBackupStore"/>), затем удаление. Без успешного бэкапа
/// удаление не выполняется (ADR 0002).
/// </summary>
public sealed class RegistryKeyDeleteFix : IFix
{
    private readonly RegistryKeyBackupStore _backup;
    private readonly string _hive;
    private readonly string _subKey;

    public RegistryKeyDeleteFix(string findingId, string hive, string subKey, RegistryKeyBackupStore backup)
    {
        FindingId = findingId;
        _hive = hive;
        _subKey = subKey;
        _backup = backup;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Registry;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // reg.exe export требует полное имя куста (HKEY_LOCAL_MACHINE), а не короткое (HKLM).
            var fullKeyPath = $@"{RegistryHiveNames.ToFullName(_hive)}\{_subKey}";
            var backupId = _backup.Backup(fullKeyPath, "Удаление записи реестра: " + _subKey);
            if (backupId is null)
            {
                return Task.FromResult(FixOutcome.Failed("Не удалось сделать бэкап ветки реестра — изменение отменено."));
            }

            var hive = RegistryHiveNames.ToHive(_hive);
            var separator = _subKey.LastIndexOf('\\');
            var parentPath = separator > 0 ? _subKey[..separator] : string.Empty;
            var childName = separator > 0 ? _subKey[(separator + 1)..] : _subKey;

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var parent = baseKey.OpenSubKey(parentPath, writable: true);
            parent?.DeleteSubKeyTree(childName, throwOnMissingSubKey: false);

            return Task.FromResult(FixOutcome.Ok(backupId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось удалить запись реестра: " + ex.Message));
        }
    }
}
