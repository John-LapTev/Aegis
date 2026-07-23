using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;

namespace Aegis.System.Fixing;

/// <summary>
/// Обратимое исправление через значение реестра: сначала сохраняет прежнее значение
/// (<see cref="RegistryBackupStore"/>), затем записывает новое. Откат — восстановлением бэкапа.
/// Заодно снимает групповую политику, которая перебила бы правку (логика в <see cref="RegistryValuesFix"/>).
/// </summary>
public sealed class RegistryValueFix : IFix
{
    private readonly RegistryValuesFix _inner;

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
        _inner = new RegistryValuesFix(
            store,
            findingId,
            group,
            [new RegistryValueEdit(hive, subKey, valueName, newValue, kind)],
            description,
            requiresReboot);
    }

    public string FindingId => _inner.FindingId;

    public ScanGroup Group => _inner.Group;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default) =>
        _inner.ApplyAsync(cancellationToken);
}
