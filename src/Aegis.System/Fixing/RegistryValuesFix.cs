using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Обратимое исправление, меняющее НЕСКОЛЬКО значений реестра одной кнопкой (например, включение брандмауэра
/// сразу в трёх профилях сети). Все прежние значения сохраняются одной записью бэкапа ПЕРЕД правкой, поэтому
/// «Вернуть» откатывает правку целиком, а не частично.
///
/// Дополнительно снимает групповые политики, которые перебивают правку (см. <see cref="PolicyOverrideCatalog"/>):
/// иначе значение запишется, а Windows продолжит слушаться политики — и «Исправлено» будет неправдой.
/// </summary>
public sealed class RegistryValuesFix : IFix
{
    private readonly RegistryBackupStore _store;
    private readonly IReadOnlyList<RegistryValueEdit> _edits;
    private readonly string _description;
    private readonly bool _requiresReboot;

    public RegistryValuesFix(
        RegistryBackupStore store,
        string findingId,
        ScanGroup group,
        IReadOnlyList<RegistryValueEdit> edits,
        string description,
        bool requiresReboot = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0)
        {
            throw new ArgumentException("Нужна хотя бы одна правка значения реестра.", nameof(edits));
        }

        _store = store;
        FindingId = findingId;
        Group = group;
        _edits = WithPolicyOverridesRemoved(edits);
        _description = description;
        _requiresReboot = requiresReboot;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    /// <summary>Полный список правок вместе со снятием мешающих политик — для тестов и диагностики.</summary>
    internal IReadOnlyList<RegistryValueEdit> Edits => _edits;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Бэкап ПЕРЕД правкой — обратимость (ADR 0002/0004). Один id на всю группу значений, включая
            // снятые политики: откат вернёт и их.
            var backupId = _store.BackupMany(
                _edits.Select(e => new RegistryValueRef(e.Hive, e.SubKey, e.ValueName)).ToList(),
                _description);

            foreach (var edit in _edits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var baseKey = RegistryKey.OpenBaseKey(edit.Hive, RegistryView.Default);

                if (edit.Value is null)
                {
                    // Снятие политики: ключа может не быть вовсе — тогда и снимать нечего.
                    using var existing = baseKey.OpenSubKey(edit.SubKey, writable: true);
                    existing?.DeleteValue(edit.ValueName, throwOnMissingValue: false);
                    continue;
                }

                using var key = baseKey.CreateSubKey(edit.SubKey, writable: true);
                key.SetValue(edit.ValueName, edit.Value, edit.Kind);
            }

            return Task.FromResult(FixOutcome.Ok(backupId, _requiresReboot));
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось применить исправление: " + ex.Message));
        }
    }

    /// <summary>
    /// Дополняет список правок снятием политик, перебивающих эти настройки. Политика снимается только там,
    /// где мы задаём значение (не при снятии другой политики), и не дублируется.
    /// </summary>
    private static IReadOnlyList<RegistryValueEdit> WithPolicyOverridesRemoved(IReadOnlyList<RegistryValueEdit> edits)
    {
        var result = new List<RegistryValueEdit>(edits);
        var seen = new HashSet<string>(
            edits.Select(e => $"{e.Hive}|{e.SubKey}|{e.ValueName}"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var edit in edits)
        {
            if (edit.Value is null)
            {
                continue;
            }

            foreach (var policy in PolicyOverrideCatalog.OverridesFor(edit.Hive, edit.SubKey, edit.ValueName))
            {
                if (seen.Add($"{policy.Hive}|{policy.SubKey}|{policy.ValueName}"))
                {
                    result.Add(new RegistryValueEdit(policy.Hive, policy.SubKey, policy.ValueName, Value: null));
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Одна правка значения реестра в составе группового исправления. <c>Value = null</c> означает «удалить
/// значение» — так снимаются групповые политики, мешающие настройке подействовать.
/// </summary>
public sealed record RegistryValueEdit(
    RegistryHive Hive,
    string SubKey,
    string ValueName,
    object? Value,
    RegistryValueKind Kind = RegistryValueKind.DWord);
