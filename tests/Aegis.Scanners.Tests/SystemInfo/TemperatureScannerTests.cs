using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class TemperatureScannerTests
{
    [Theory]
    [InlineData("Процессор", 50, Severity.Ok)]
    [InlineData("Процессор", 88, Severity.Warning)]   // CPU: warn ≥85
    [InlineData("Процессор", 97, Severity.Danger)]    // CPU: danger ≥95
    [InlineData("Видеокарта", 60, Severity.Ok)]
    [InlineData("Видеокарта", 84, Severity.Warning)]  // GPU пороги ниже: warn ≥80
    [InlineData("Видеокарта", 92, Severity.Danger)]   // GPU: danger ≥90
    public async Task ScanAsync_ClassifiesTemperatureBySeverity(string component, int celsius, Severity expected)
    {
        var scanner = new TemperatureScanner(new FakeProbe(
            [new TemperatureReading { Component = component, Celsius = celsius }]));

        var result = await scanner.ScanAsync();

        Assert.Equal(expected, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public async Task ScanAsync_NoReadings_ReturnsInfoNoSensors()
    {
        var scanner = new TemperatureScanner(new FakeProbe([]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal("temp-none", finding.Id);
    }

    [Fact]
    public async Task ScanAsync_GpuHotterThreshold_ThanCpu_AtSameTemperature()
    {
        // 84 °C: для видеокарты — уже «Внимание», для процессора — ещё «норма».
        var gpu = await new TemperatureScanner(new FakeProbe(
            [new TemperatureReading { Component = "Видеокарта", Celsius = 84 }])).ScanAsync();
        var cpu = await new TemperatureScanner(new FakeProbe(
            [new TemperatureReading { Component = "Процессор", Celsius = 84 }])).ScanAsync();

        Assert.Equal(Severity.Warning, Assert.Single(gpu.Findings).Severity);
        Assert.Equal(Severity.Ok, Assert.Single(cpu.Findings).Severity);
    }

    private sealed class FakeProbe(IReadOnlyList<TemperatureReading> readings) : ITemperatureProbe
    {
        public Task<IReadOnlyList<TemperatureReading>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(readings);
    }
}
