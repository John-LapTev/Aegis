using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Включение «Защиты системы» (точек восстановления) на системном диске — это «зонтик» обратимости, на
/// который опирается вся починка (ADR 0002). Обратимо: прежнее значение политики DisableSR сохраняется ПЕРЕД
/// правкой; затем снимается блокировка политикой и включается защита через <c>Enable-ComputerRestore</c>.
/// </summary>
public sealed class SystemRestoreEnableFix : IFix
{
    private const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";

    private readonly RegistryBackupStore _backup;

    public SystemRestoreEnableFix(string findingId, RegistryBackupStore backup)
    {
        FindingId = findingId;
        _backup = backup;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.System;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        // 1) Бэкап прежнего состояния политики (если значения нет — откат удалит его обратно).
        var backupId = _backup.Backup(RegistryHive.LocalMachine, PolicyKey, "DisableSR",
            "Включение защиты системы (точек восстановления)");

        // 2) Снять блокировку защиты политикой, если она была.
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
            using var key = baseKey.CreateSubKey(PolicyKey, writable: true);
            key.SetValue("DisableSR", 0, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            return FixOutcome.Failed("Не удалось снять блокировку защиты системы: " + ex.Message);
        }

        // 3) Включить защиту на системном диске.
        var drive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var code = await ProcessRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"Enable-ComputerRestore -Drive '{drive}'\"",
            cancellationToken).ConfigureAwait(false);

        if (code != 0)
        {
            return FixOutcome.Failed(
                "Не удалось включить защиту системы автоматически. Включи её вручную: «Свойства системы» → " +
                "«Защита системы» → выбери системный диск → «Настроить» → «Включить защиту системы».");
        }

        return FixOutcome.Ok(backupId);
    }
}
