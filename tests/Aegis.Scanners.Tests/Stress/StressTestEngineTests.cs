using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Stress;
using Xunit;

namespace Aegis.Scanners.Tests.Stress;

public sealed class StressTestEngineTests
{
    // Короткий тест + мгновенная задержка — чтобы юнит-тесты не ждали реальные секунды.
    private static readonly StressTestOptions FastOptions = new() { SafeSeconds = 3, SamplingIntervalMs = 1000 };
    private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;

    [Fact]
    public async Task RunAsync_CoolMachine_CompletesWithOkVerdict_AndStopsLoad()
    {
        var load = new FakeCpuLoad();
        var engine = new StressTestEngine(load, new SequenceTempProbe((70, 60)), FastOptions, NoDelay);

        var result = await engine.RunAsync(StressTestKind.CpuSafe);

        Assert.Equal(StressAbortReason.Completed, result.Reason);
        Assert.Equal(Severity.Ok, result.Severity);
        Assert.Equal(70, result.MaxCpuCelsius);
        Assert.Equal(3, result.DurationSeconds);
        Assert.False(result.ThrottlingLikely);
        Assert.Equal(1, load.StartCount);
        Assert.True(load.Disposed); // нагрузка обязательно снимается
    }

    [Fact]
    public async Task RunAsync_Overheats_AutoStops_WithDangerVerdict()
    {
        var load = new FakeCpuLoad();
        // Температура растёт: 70 → 85 → 96 (≥ порога 95) → авто-стоп.
        var engine = new StressTestEngine(load, new SequenceTempProbe((70, null), (85, null), (96, null)), FastOptions, NoDelay);

        var result = await engine.RunAsync(StressTestKind.CpuSafe);

        Assert.Equal(StressAbortReason.OverheatStopped, result.Reason);
        Assert.Equal(Severity.Danger, result.Severity);
        Assert.Equal(96, result.MaxCpuCelsius);
        Assert.True(result.ThrottlingLikely);
        Assert.True(load.Disposed);
        Assert.Contains("остановил тест", result.Verdict);
    }

    [Fact]
    public async Task RunAsync_WarmButSafe_CompletesWithWarning()
    {
        var load = new FakeCpuLoad();
        // 90 °C: выше «грелся сильно» (88), но ниже авто-стопа (95).
        var engine = new StressTestEngine(load, new SequenceTempProbe((90, 70)), FastOptions, NoDelay);

        var result = await engine.RunAsync(StressTestKind.CpuSafe);

        Assert.Equal(StressAbortReason.Completed, result.Reason);
        Assert.Equal(Severity.Warning, result.Severity);
        Assert.Equal(90, result.MaxCpuCelsius);
    }

    [Fact]
    public async Task RunAsync_NoSensors_StillCompletes_WithInfoVerdict()
    {
        var load = new FakeCpuLoad();
        var engine = new StressTestEngine(load, new SequenceTempProbe((null, null)), FastOptions, NoDelay);

        var result = await engine.RunAsync(StressTestKind.CpuSafe);

        Assert.Equal(StressAbortReason.Completed, result.Reason);
        Assert.Equal(Severity.Info, result.Severity);
        Assert.Null(result.MaxCpuCelsius);
        Assert.True(load.Disposed);
    }

    [Fact]
    public async Task RunAsync_Cancelled_ReportsCancelled_AndStopsLoad()
    {
        var load = new FakeCpuLoad();
        using var cts = new CancellationTokenSource();
        var delays = 0;
        Func<TimeSpan, CancellationToken, Task> cancelAfterTwo = (_, ct) =>
        {
            if (++delays >= 2)
            {
                cts.Cancel();
            }

            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        var engine = new StressTestEngine(load, new SequenceTempProbe((70, 60)), FastOptions, cancelAfterTwo);

        var result = await engine.RunAsync(StressTestKind.CpuSafe, progress: null, cancellationToken: cts.Token);

        Assert.Equal(StressAbortReason.Cancelled, result.Reason);
        Assert.Equal(Severity.Info, result.Severity);
        Assert.True(load.Disposed);
    }

    [Fact]
    public async Task RunAsync_ReportsLiveProgress_EachStep()
    {
        var load = new FakeCpuLoad();
        var engine = new StressTestEngine(load, new SequenceTempProbe((70, 60)), FastOptions, NoDelay);
        var collector = new SyncProgress();

        await engine.RunAsync(StressTestKind.CpuSafe, collector);

        // Шаги 0,1,2,3 секунды — четыре замера; у каждого план = 3 с.
        Assert.Equal(4, collector.Reports.Count);
        Assert.All(collector.Reports, r => Assert.Equal(3, r.PlannedSeconds));
        Assert.Equal(0, collector.Reports[0].ElapsedSeconds);
        Assert.Equal(3, collector.Reports[^1].ElapsedSeconds);
        Assert.Equal(70, collector.Reports[^1].MaxCpuCelsius);
    }

    private sealed class SyncProgress : IProgress<StressTestProgress>
    {
        public List<StressTestProgress> Reports { get; } = [];

        public void Report(StressTestProgress value) => Reports.Add(value);
    }

    private sealed class FakeCpuLoad : ICpuLoad
    {
        public int StartCount { get; private set; }

        public bool Disposed { get; private set; }

        public IDisposable Start(int? threadCount = null)
        {
            StartCount++;
            return new Stopper(this);
        }

        private sealed class Stopper(FakeCpuLoad owner) : IDisposable
        {
            public void Dispose() => owner.Disposed = true;
        }
    }

    private sealed class SequenceTempProbe : ITemperatureProbe
    {
        private readonly Queue<(int? Cpu, int? Gpu)> _sequence;
        private readonly (int? Cpu, int? Gpu) _last;

        public SequenceTempProbe(params (int? Cpu, int? Gpu)[] sequence)
        {
            _sequence = new Queue<(int? Cpu, int? Gpu)>(sequence);
            _last = sequence.Length > 0 ? sequence[^1] : (null, null);
        }

        public Task<IReadOnlyList<TemperatureReading>> ReadAsync(CancellationToken cancellationToken = default)
        {
            var value = _sequence.Count > 0 ? _sequence.Dequeue() : _last;
            var list = new List<TemperatureReading>();
            if (value.Cpu is int c)
            {
                list.Add(new TemperatureReading { Component = "Процессор", Celsius = c });
            }

            if (value.Gpu is int g)
            {
                list.Add(new TemperatureReading { Component = "Видеокарта", Celsius = g });
            }

            return Task.FromResult<IReadOnlyList<TemperatureReading>>(list);
        }
    }
}
