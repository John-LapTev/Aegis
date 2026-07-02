using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;

namespace Aegis.System.Fixing;

/// <summary>
/// Обратимое исправление через значение реестра: сначала сохраняет прежнее значение
/// (<see cref="RegistryBackupStore"/>), затем записывает новое. Откат — восстановлением бэкапа.
/// </summary>
public sealed class RegistryValueFix : IFix
{
    private readonly RegistryBackupStore _store;
    private readonly RegistryHive _hive;
    private readonly string _subKey;
    private readonly string _valueName;
    private readonly object _newValue;
    private readonly RegistryValueKind _kind;
    private readonly string _description;
    private readonly bool _requiresReboot;

    public RegistryValueFix(
        RegistryBackupStore store,
        string findingId,
        ScanGroup group,
        RegistryHive hive,
        string subKey,
        string valueName,
        object newValue,
        RegistryValueKind kind,
        string description,
        bool requiresReboot = false)
    {
        _store = store;
        FindingId = findingId;
        Group = group;
        _hive = hive;
        _subKey = subKey;
        _valueName = valueName;
        _newValue = newValue;
        _kind = kind;
        _description = description;
        _requiresReboot = requiresReboot;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Бэкап ПЕРЕД правкой — обратимость (ADR 0002/0004).
            var backupId = _store.Backup(_hive, _subKey, _valueName, _description);

            using var baseKey = RegistryKey.OpenBaseKey(_hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(_subKey, writable: true);
            key.SetValue(_valueName, _newValue, _kind);

            return Task.FromResult(FixOutcome.Ok(backupId, _requiresReboot));
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось применить исправление: " + ex.Message));
        }
    }
}
