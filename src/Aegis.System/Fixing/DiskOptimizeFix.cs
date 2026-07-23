using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Запускает штатное обслуживание дисков Windows (<c>defrag /C /O</c>): для твердотельных — команду TRIM,
/// для обычных — дефрагментацию, каждому диску своё. Файлы не меняются и не удаляются, поэтому «откат» тут
/// не нужен и не обещается (результат — «без бэкапа»).
/// </summary>
public sealed class DiskOptimizeFix : IFix
{
    /// <summary>Дефрагментация большого диска идёт долго; больше часа ждать не станем.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromHours(1);

    public DiskOptimizeFix(string findingId, ScanGroup group)
    {
        FindingId = findingId;
        Group = group;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            // /C — все диски, /O — правильная операция для каждого типа носителя (TRIM или дефрагментация).
            var code = await ProcessRunner
                .RunAsync(ProcessRunner.System("defrag.exe"), "/C /O", timeout.Token)
                .ConfigureAwait(false);

            return code == 0
                ? FixOutcome.OkWithoutBackup()
                : FixOutcome.Failed(
                    "Windows не смогла выполнить обслуживание дисков. Обычно это значит, что нет прав " +
                    "администратора или диск сейчас занят другой задачей — попробуй позже.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FixOutcome.Failed(
                "Обслуживание дисков идёт слишком долго — я его остановил. Windows продолжит делать это сама " +
                "по расписанию.");
        }
    }
}
