using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Stress;

/// <summary>
/// Проводит безопасную проверку под нагрузкой: запускает нагрузку процессора, раз в шаг замеряет температуру,
/// сам останавливается по предохранителям (температура у порога / истёк запланированный срок) или по «Стоп».
/// Никогда не доводит до опасного нагрева. Логика чистая и тестируемая: нагрузка и датчик — за абстракциями,
/// задержка между замерами — внедряемая (в тестах мгновенная).
/// </summary>
public sealed class StressTestEngine : IStressTestEngine
{
    private readonly ICpuLoad _cpuLoad;
    private readonly ITemperatureProbe _temperatureProbe;
    private readonly StressTestOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public StressTestEngine(
        ICpuLoad cpuLoad,
        ITemperatureProbe temperatureProbe,
        StressTestOptions? options = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(cpuLoad);
        ArgumentNullException.ThrowIfNull(temperatureProbe);
        _cpuLoad = cpuLoad;
        _temperatureProbe = temperatureProbe;
        _options = options ?? new StressTestOptions();
        _delay = delay ?? Task.Delay;
    }

    public async Task<StressTestResult> RunAsync(
        StressTestKind kind,
        IProgress<StressTestProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var planned = _options.PlannedSeconds(kind);
        var interval = TimeSpan.FromMilliseconds(_options.SamplingIntervalMs);
        var stepSeconds = Math.Max(1, _options.SamplingIntervalMs / 1000);

        int? maxCpu = null, maxGpu = null;
        var elapsed = 0;
        var reason = StressAbortReason.Completed;

        using var load = _cpuLoad.Start(_options.ThreadCount);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (cpu, gpu) = await ReadTemperaturesAsync(cancellationToken).ConfigureAwait(false);
                maxCpu = Hotter(maxCpu, cpu);
                maxGpu = Hotter(maxGpu, gpu);

                progress?.Report(new StressTestProgress
                {
                    ElapsedSeconds = elapsed,
                    PlannedSeconds = planned,
                    CpuCelsius = cpu,
                    GpuCelsius = gpu,
                    MaxCpuCelsius = maxCpu,
                    MaxGpuCelsius = maxGpu,
                });

                if (IsTooHot(cpu, gpu))
                {
                    reason = StressAbortReason.OverheatStopped;
                    break;
                }

                if (elapsed >= planned)
                {
                    reason = StressAbortReason.Completed;
                    break;
                }

                await _delay(interval, cancellationToken).ConfigureAwait(false);
                elapsed += stepSeconds;
            }
        }
        catch (OperationCanceledException)
        {
            reason = StressAbortReason.Cancelled;
        }

        var throttling = reason == StressAbortReason.OverheatStopped
                         || (maxCpu is int mc && mc >= _options.CpuAbortCelsius);
        var (severity, verdict) = BuildVerdict(reason, maxCpu, maxGpu);

        return new StressTestResult
        {
            Kind = kind,
            Reason = reason,
            MaxCpuCelsius = maxCpu,
            MaxGpuCelsius = maxGpu,
            DurationSeconds = elapsed,
            ThrottlingLikely = throttling,
            Severity = severity,
            Verdict = verdict,
        };
    }

    private async Task<(int? Cpu, int? Gpu)> ReadTemperaturesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var readings = await _temperatureProbe.ReadAsync(cancellationToken).ConfigureAwait(false);
            int? cpu = null, gpu = null;
            foreach (var reading in readings)
            {
                if (IsGpu(reading.Component))
                {
                    gpu = reading.Celsius;
                }
                else
                {
                    cpu = reading.Celsius;
                }
            }

            return (cpu, gpu);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Датчик не отдал данные на этом шаге — продолжаем без замера (не валим тест).
            return (null, null);
        }
    }

    private bool IsTooHot(int? cpu, int? gpu) =>
        (cpu is int c && c >= _options.CpuAbortCelsius) || (gpu is int g && g >= _options.GpuAbortCelsius);

    private static bool IsGpu(string component) =>
        component.Contains("видео", StringComparison.OrdinalIgnoreCase)
        || component.Contains("карт", StringComparison.OrdinalIgnoreCase);

    private static int? Hotter(int? current, int? candidate) =>
        candidate is int c && (current is null || c > current) ? c : current;

    /// <summary>Вердикт простыми словами по итогу теста. Чистая логика — проверяется тестами.</summary>
    private (Severity Severity, string Verdict) BuildVerdict(StressAbortReason reason, int? maxCpu, int? maxGpu)
    {
        var peak = PeakText(maxCpu, maxGpu);

        if (reason == StressAbortReason.OverheatStopped)
        {
            return (Severity.Danger,
                $"Компьютер сильно нагрелся{peak} — я остановил тест ради безопасности. Это значит, что под нагрузкой " +
                "охлаждение не справляется: компьютер будет тормозить (сбрасывать скорость), шуметь и быстрее " +
                "изнашиваться. Почисти вентиляторы от пыли, проверь, не закрыты ли отверстия охлаждения; ноутбук " +
                "поставь на твёрдую ровную поверхность или подставку, чтобы снизу проходил воздух.");
        }

        if (reason == StressAbortReason.Cancelled)
        {
            return (Severity.Info, $"Тест остановлен вручную{peak}. Это нормально — можно запустить заново в любой момент.");
        }

        if (reason == StressAbortReason.Error)
        {
            return (Severity.Info, "Не получилось провести проверку под нагрузкой. Это не значит, что с компьютером что-то не так.");
        }

        // Completed — прошёл полностью. Оценка по пиковой температуре.
        var hotCpu = maxCpu is int mc && mc >= _options.CpuWarnCelsius;
        var hotGpu = maxGpu is int mg && mg >= _options.GpuWarnCelsius;

        if (maxCpu is null && maxGpu is null)
        {
            return (Severity.Info,
                "Проверка под нагрузкой прошла полностью, компьютер выдержал. Температуру измерить не удалось " +
                "(это железо не отдаёт датчики) — но раз тест завершился без сбоев, всё в порядке.");
        }

        if (hotCpu || hotGpu)
        {
            return (Severity.Warning,
                $"Проверка прошла, компьютер выдержал, но под полной нагрузкой грелся заметно{peak}. Пока терпимо, " +
                "но в играх и тяжёлых программах стоит последить за температурой. Не помешает почистить компьютер " +
                "от пыли и улучшить охлаждение.");
        }

        return (Severity.Ok,
            $"Отлично: под полной нагрузкой компьютер держался спокойно{peak}. Охлаждение справляется, " +
            "беспокоиться не о чем.");
    }

    /// <summary>Хвост вида « (процессор до 78 °C, видеокарта до 65 °C)» — или пусто, если датчиков не было.</summary>
    private static string PeakText(int? maxCpu, int? maxGpu)
    {
        var parts = new List<string>();
        if (maxCpu is int c)
        {
            parts.Add($"процессор до {c} °C");
        }

        if (maxGpu is int g)
        {
            parts.Add($"видеокарта до {g} °C");
        }

        return parts.Count == 0 ? string.Empty : " (" + string.Join(", ", parts) + ")";
    }
}
