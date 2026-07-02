using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Сброс сетевых настроек к стандартным (чинит частые проблемы с интернетом): очистка кэша DNS,
/// сброс Winsock и стека TCP/IP. Штатные команды Windows. После сброса нужна перезагрузка.
/// </summary>
public sealed class NetworkResetFix : IFix
{
    public NetworkResetFix(string findingId) => FindingId = findingId;

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.System;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ProcessRunner.RunAsync(ProcessRunner.System("ipconfig.exe"), "/flushdns", cancellationToken).ConfigureAwait(false);
            var winsock = await ProcessRunner.RunAsync(ProcessRunner.System("netsh.exe"), "winsock reset", cancellationToken).ConfigureAwait(false);
            await ProcessRunner.RunAsync(ProcessRunner.System("netsh.exe"), "int ip reset", cancellationToken).ConfigureAwait(false);

            if (winsock < 0)
            {
                return FixOutcome.Failed("Не удалось выполнить сброс сети.");
            }

            MaintenanceHistory.MarkRun(MaintenanceHistory.NetworkResetKey); // запомнить «запускали» → покажем дату
            return FixOutcome.OkWithoutBackup(requiresReboot: true);
        }
        catch (OperationCanceledException)
        {
            throw; // отмена пользователем — пусть дойдёт как «Операция отменена»
        }
        catch (Exception ex)
        {
            return FixOutcome.Failed("Не удалось сбросить сеть: " + ex.Message);
        }
    }
}
