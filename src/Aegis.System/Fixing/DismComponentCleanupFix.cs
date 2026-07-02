using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Очистка хранилища компонентов Windows штатным DISM
/// (<c>dism /online /cleanup-image /startcomponentcleanup</c>) — удаляет устаревшие версии обновлений.
/// Безопасное обслуживание Microsoft; пользовательских данных не касается, поэтому отдельный бэкап не нужен.
/// </summary>
public sealed class DismComponentCleanupFix : IFix
{
    public DismComponentCleanupFix(string findingId) => FindingId = findingId;

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var code = await ProcessRunner.RunAsync(
            ProcessRunner.System("Dism.exe"), "/online /cleanup-image /startcomponentcleanup", cancellationToken)
            .ConfigureAwait(false);

        return code switch
        {
            0 => FixOutcome.OkWithoutBackup(),
            // 3010 = ERROR_SUCCESS_REBOOT_REQUIRED: очистка выполнена, нужна перезагрузка — это УСПЕХ, не ошибка.
            3010 => FixOutcome.OkWithoutBackup(requiresReboot: true),
            < 0 => FixOutcome.Failed("Не удалось запустить очистку (DISM)."),
            _ => FixOutcome.Failed($"Очистка завершилась с кодом {code}."),
        };
    }
}
