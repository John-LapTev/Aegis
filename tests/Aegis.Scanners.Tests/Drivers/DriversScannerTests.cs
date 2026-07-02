using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Drivers;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Drivers;

public sealed class DriversScannerTests
{
    [Fact]
    public async Task ScanAsync_ModelDeviceAndGpu_ProducesExpectedFindings()
    {
        var snapshot = new DriverSnapshot
        {
            Manufacturer = "Acme",
            Model = "Laptop X",
            ProblemDevices = [new ProblemDevice { Name = "Тачпад", DeviceId = @"ACPI\TP\1", ErrorCode = 28 }],
            DisabledDevices = [],
            InstalledDrivers = [],
            GraphicsCards = [new GraphicsCard { Name = "NVIDIA RTX", DriverVersion = "1.0" }],
        };
        var scanner = new DriversScanner(new FakeProbe(snapshot), new FakeNvidiaCheck(), new FakeLookup());

        var result = await scanner.ScanAsync();

        Assert.Equal(ScanGroup.Drivers, result.Group);
        Assert.Equal(3, result.Findings.Count); // модель (Ok) + устройство (Warning) + видеокарта (Ok)

        var device = Assert.Single(result.Findings, f => f.Severity == Severity.Warning);
        Assert.NotNull(device.Data);
        Assert.Equal("driver-search", device.Data!["kind"]);
        Assert.Equal(@"ACPI\TP\1", device.Data!["deviceId"]);
    }

    [Fact]
    public async Task ScanAsync_UnknownModelAndNoDevices_ShowsOnlyGpu()
    {
        var snapshot = new DriverSnapshot
        {
            Manufacturer = string.Empty,
            Model = string.Empty,
            ProblemDevices = [],
            DisabledDevices = [],
            InstalledDrivers = [],
            GraphicsCards = [new GraphicsCard { Name = "Intel UHD" }],
        };
        var scanner = new DriversScanner(new FakeProbe(snapshot), new FakeNvidiaCheck(), new FakeLookup());

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.StartsWith("driver-gpu-", finding.Id);
    }

    private sealed class FakeProbe(DriverSnapshot snapshot) : IDriverProbe
    {
        public Task<DriverSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class FakeNvidiaCheck : INvidiaDriverCheck
    {
        public Task<DriverUpdate?> CheckAsync(string gpuName, string? installedVersion, CancellationToken cancellationToken = default) =>
            Task.FromResult<DriverUpdate?>(null);
    }

    private sealed class FakeLookup : IDeviceUpdateLookup
    {
        public Task<DeviceUpdateResult> LookupAsync(string deviceName, DeviceLookupKind kind = DeviceLookupKind.Driver, CancellationToken cancellationToken = default) =>
            Task.FromResult(DeviceUpdateResult.Empty);
    }
}
