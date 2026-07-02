using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Недавние синие экраны (группа <see cref="ScanGroup.Health"/>): плитка «Стабильность» — были ли за неделю
/// сбои (BSOD). Ноль — отлично; есть — предупреждаем и подсказываем частые причины простыми словами.
/// Только показывает.
/// </summary>
public sealed class CrashHistoryScanner : IScanner
{
    private readonly ICrashHistoryProbe _probe;

    public CrashHistoryScanner(ICrashHistoryProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Health;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var count = await _probe.RecentCrashCountAsync(cancellationToken).ConfigureAwait(false);

        var finding = count == 0
            ? new Finding
            {
                Id = "health-crashes",
                Group = ScanGroup.Health,
                Severity = Severity.Ok,
                Title = "Стабильность",
                Detail = "сбоев нет",
                Explain = "За последнюю неделю синих экранов (внезапных сбоев Windows) не было — система работает стабильно.",
                Data = new Dictionary<string, string> { ["healthIcon"] = "shield", ["metric"] = "0", ["metricLabel"] = "сбоев" },
            }
            : new Finding
            {
                Id = "health-crashes",
                Group = ScanGroup.Health,
                Severity = count >= 3 ? Severity.Danger : Severity.Warning,
                Title = "Были синие экраны",
                Detail = $"{count} за неделю",
                Explain = $"За последнюю неделю было синих экранов (сбоев с перезагрузкой): {count}. Частые причины — " +
                          "проблемный или устаревший драйвер, перегрев, реже — сбойная планка памяти. Что стоит сделать: " +
                          "обнови драйверы (вкладка «Драйверы»), проверь температуры (здесь же, в «Здоровье»), при " +
                          "повторении — стоит показать компьютер специалисту. Если сбой был один и давно — скорее всего, случайность.",
                Data = new Dictionary<string, string>
                {
                    ["healthIcon"] = "shield",
                    ["metric"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["metricLabel"] = "за неделю",
                },
            };

        return new ScanResult { Group = ScanGroup.Health, Findings = [finding] };
    }
}
