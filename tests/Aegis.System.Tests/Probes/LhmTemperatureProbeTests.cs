using Aegis.Scanners.Probing;
using Aegis.System.Probes;
using Xunit;

namespace Aegis.System.Tests.Probes;

public sealed class LhmTemperatureProbeTests
{
    [Fact]
    public async Task UsesLhmValues_WhenBothPresent_NoFallback()
    {
        var fallback = new CountingFallback(cpu: 45, gpu: 40);
        var probe = new LhmTemperatureProbe(new FakeSensors(new HardwareReadings { CpuTempCelsius = 72, GpuTempCelsius = 58 }), fallback);

        var readings = await probe.ReadAsync();

        Assert.Equal(72, Temp(readings, "Процессор"));
        Assert.Equal(58, Temp(readings, "Видеокарта"));
        Assert.Equal(0, fallback.Calls); // LHM хватило — стандартный датчик не трогаем
    }

    [Fact]
    public async Task FallsBackPerComponent_WhenLhmMissing()
    {
        var fallback = new CountingFallback(cpu: 44, gpu: 61);
        // LHM дал только видеокарту; процессор — из стандартного датчика.
        var probe = new LhmTemperatureProbe(new FakeSensors(new HardwareReadings { GpuTempCelsius = 59 }), fallback);

        var readings = await probe.ReadAsync();

        Assert.Equal(44, Temp(readings, "Процессор"));   // из fallback
        Assert.Equal(59, Temp(readings, "Видеокарта"));  // из LHM
        Assert.Equal(1, fallback.Calls);
    }

    [Fact]
    public async Task Empty_WhenNeitherSourceHasData()
    {
        var probe = new LhmTemperatureProbe(new FakeSensors(HardwareReadings.Empty), new CountingFallback(null, null));

        var readings = await probe.ReadAsync();

        Assert.Empty(readings);
    }

    private static int? Temp(IReadOnlyList<TemperatureReading> readings, string component) =>
        readings.FirstOrDefault(r => r.Component == component)?.Celsius;

    private sealed class FakeSensors(HardwareReadings readings) : IHardwareSensorReader
    {
        public HardwareReadings Read() => readings;
    }

    private sealed class CountingFallback(int? cpu, int? gpu) : ITemperatureProbe
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<TemperatureReading>> ReadAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            var list = new List<TemperatureReading>();
            if (cpu is not null)
            {
                list.Add(new TemperatureReading { Component = "Процессор", Celsius = cpu });
            }

            if (gpu is not null)
            {
                list.Add(new TemperatureReading { Component = "Видеокарта", Celsius = gpu });
            }

            return Task.FromResult<IReadOnlyList<TemperatureReading>>(list);
        }
    }
}
