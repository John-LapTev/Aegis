using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Обновляет все программы, у которых вышла новая версия, через встроенный установщик Windows
/// (<c>winget upgrade --all</c>). Отката средствами программы нет: вернуть предыдущую версию можно только
/// установив её вручную — поэтому результат помечается как «без бэкапа», и кнопка «Вернуть» не появляется.
/// </summary>
public sealed class ProgramUpgradeAllFix : IFix
{
    /// <summary>Обновление всех программ может идти долго — даём запас, но не бесконечный.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    public ProgramUpgradeAllFix(string findingId, ScanGroup group)
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
            // --silent: без окон установщиков; согласия — чтобы установка не ждала ответа в невидимом окне.
            var code = await ProcessRunner.RunAsync(
                "winget",
                "upgrade --all --silent --accept-package-agreements --accept-source-agreements",
                timeout.Token).ConfigureAwait(false);

            // Часть программ может отказаться обновляться тихо (например, требует закрытия). Код возврата у
            // winget в таком случае не нулевой, но остальные программы уже обновлены — сообщаем честно.
            return code == 0
                ? FixOutcome.OkWithoutBackup()
                : new FixOutcome
                {
                    Success = true,
                    BackupId = null,
                    Message = "Часть программ обновить не удалось — обычно это те, что сейчас запущены. " +
                              "Закрой их и нажми обновление ещё раз.",
                };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FixOutcome.Failed(
                "Обновление программ идёт слишком долго — я его остановил. Попробуй ещё раз, когда сеть будет свободнее.");
        }
    }
}
