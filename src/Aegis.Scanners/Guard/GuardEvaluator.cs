using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Guard;

/// <summary>
/// «Мозг» фонового стража: по текущему списку процессов и тому, как давно человек не трогал компьютер,
/// решает, о чём стоит предупредить. Главный сигнал реального времени — программа без подписи грузит
/// процессор, ПОКА человек отошёл (или прячется в служебной папке / со странным именем): так ведёт себя
/// скрытый майнер, которого обычная ручная проверка не поймает (в момент проверки человек за компьютером).
/// Чистая логика без обращения к системе — тестируется целиком.
/// </summary>
public sealed class GuardEvaluator
{
    /// <summary>Простой пользователя, после которого считаем, что он отошёл.</summary>
    private static readonly TimeSpan AwayThreshold = TimeSpan.FromMinutes(5);

    public IReadOnlyList<GuardAlert> Evaluate(IReadOnlyList<ProcessInfo> processes, TimeSpan idle)
    {
        ArgumentNullException.ThrowIfNull(processes);

        var userAway = idle >= AwayThreshold;
        var alerts = new List<GuardAlert>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in processes)
        {
            // Кандидат — неподписанная программа, реально грузящая процессор.
            if (process.CpuPercent < MinerHeuristics.CpuGate || !MinerHeuristics.IsUntrusted(process.Signature))
            {
                continue;
            }

            var stealth = MinerHeuristics.IsStealthPath(process.ExecutablePath);
            var randomName = MinerHeuristics.LooksRandomName(process.Name);

            // В фоне важно не спамить: тревожим только при сильном признаке
            // (прячется / странное имя / грузит, пока человека нет).
            if (!stealth && !randomName && !userAway)
            {
                continue;
            }

            var key = string.IsNullOrEmpty(process.ExecutablePath) ? process.Name : process.ExecutablePath;
            if (!seen.Add(key))
            {
                continue;
            }

            var why = userAway
                ? "грузит процессор, пока вас нет за компьютером"
                : stealth
                    ? "грузит процессор и прячется в служебной папке"
                    : "грузит процессор, а имя файла похоже на замаскированный вирус";

            alerts.Add(new GuardAlert
            {
                Severity = Severity.Danger,
                Key = "miner:" + key,
                Title = "Возможный скрытый майнер",
                Message = $"Программа «{process.Name}» {why}. Откройте Aegis и проверьте раздел «Угрозы».",
            });
        }

        return alerts;
    }
}
