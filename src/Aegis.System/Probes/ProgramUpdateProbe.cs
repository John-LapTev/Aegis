using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальная проверка обновлений программ через встроенный установщик Windows (winget). Только читает:
/// команда <c>winget upgrade</c> ничего не устанавливает, а лишь показывает список.
/// </summary>
public sealed class ProgramUpdateProbe : IProgramUpdateProbe
{
    public async Task<IReadOnlyList<AvailableUpgrade>> ReadAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            // Согласия на условия источников — чтобы команда не ждала ответа в невидимом окне и не «висла».
            var output = await ProcessRunner
                .RunForOutputAsync("winget", "upgrade --accept-source-agreements", cancellationToken)
                .ConfigureAwait(false);

            return WingetUpgradeParser.Parse(output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // winget не установлен (бывает на старых сборках Windows) — просто не показываем раздел.
            return [];
        }
    }
}
