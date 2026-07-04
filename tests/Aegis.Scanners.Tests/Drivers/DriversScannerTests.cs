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
        var scanner = new DriversScanner(new FakeProbe(snapshot), new FakeNvidiaCheck(), new FakeLookup(), new FakeCatalog());

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
        var scanner = new DriversScanner(new FakeProbe(snapshot), new FakeNvidiaCheck(), new FakeLookup(), new FakeCatalog());

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.StartsWith("driver-gpu-", finding.Id);
    }

    [Fact]
    public async Task ScanAsync_CatalogHasNewerDriver_EmitsUpdateFindingForMatchedDevice()
    {
        var snapshot = new DriverSnapshot
        {
            Manufacturer = string.Empty,
            Model = string.Empty,
            ProblemDevices = [],
            DisabledDevices = [],
            InstalledDrivers =
            [
                new DriverInfo
                {
                    DeviceName = "Realtek High Definition Audio",
                    Category = "Звук",
                    Version = "6.0.1.100",
                    Date = "2023-01-01",
                    DeviceId = @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0256\4&abc",
                },
            ],
            GraphicsCards = [],
        };
        var catalog = new FakeCatalog(new DriverUpdateOffer
        {
            Title = "Realtek - MEDIA - 6.0.1.200",
            DeviceName = "Realtek High Definition Audio",
            HardwareId = @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0256",
            Provider = "Realtek",
            Date = "2024-05-01",
            UpdateId = "abc-123",
        });
        var scanner = new DriversScanner(new FakeProbe(snapshot), new FakeNvidiaCheck(), new FakeLookup(), catalog);

        var result = await scanner.ScanAsync();

        var update = Assert.Single(result.Findings, f => f.Id.StartsWith("driver-update-", StringComparison.Ordinal));
        Assert.Equal(Severity.Info, update.Severity);
        Assert.Contains("Realtek High Definition Audio", update.Title);
        Assert.Contains("2024-05-01", update.Detail); // доступная дата видна пользователю
        // Есть идентификатор обновления → находка исправима (кнопка «Установить драйвер» ставит прямо из программы).
        Assert.Equal(FindingKinds.DriverWuInstall, update.Data!["kind"]);
        Assert.Equal("abc-123", update.Data!["updateId"]);
    }

    [Fact]
    public async Task ScanAsync_UnmatchedOffer_BecomesItsOwnInstallableFinding()
    {
        var snapshot = new DriverSnapshot
        {
            Manufacturer = string.Empty,
            Model = string.Empty,
            ProblemDevices = [],
            DisabledDevices = [],
            InstalledDrivers = [],
            GraphicsCards = [],
        };
        var catalog = new FakeCatalog(new DriverUpdateOffer
        {
            Title = "Intel - System - Chipset 10.1.2",
            DeviceName = "Intel Chipset",
            HardwareId = @"PCI\VEN_8086&DEV_1234",
            Provider = "Intel",
            Date = "2024-06-01",
            UpdateId = "chip-9",
        });
        var scanner = new DriversScanner(new FakeProbe(snapshot), new FakeNvidiaCheck(), new FakeLookup(), catalog);

        var result = await scanner.ScanAsync();

        var update = Assert.Single(result.Findings, f => f.Id.StartsWith("driver-update-", StringComparison.Ordinal));
        Assert.Contains("Intel Chipset", update.Title);
        Assert.Equal(FindingKinds.DriverWuInstall, update.Data!["kind"]);
        Assert.Equal("chip-9", update.Data!["updateId"]);
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

    private sealed class FakeCatalog(params DriverUpdateOffer[] offers) : IDriverUpdateCatalog
    {
        public Task<IReadOnlyList<DriverUpdateOffer>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DriverUpdateOffer>>(offers);

        public Task<DriverInstallResult> InstallAsync(string updateId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DriverInstallResult.Ok(requiresReboot: false));
    }
}
