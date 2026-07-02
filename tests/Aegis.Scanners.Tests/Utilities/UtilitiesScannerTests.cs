using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Utilities;
using Xunit;

namespace Aegis.Scanners.Tests.Utilities;

public sealed class UtilitiesScannerTests
{
    [Fact]
    public async Task ScanAsync_PcUtilityMissing_RecommendsInstallViaWinget()
    {
        var scanner = new UtilitiesScanner(new FakeUtilitiesProbe(new UtilitiesSnapshot
        {
            Manufacturer = "LENOVO",
            Model = "Legion 5",
            InstalledPrograms = [],
            PeripheralVendors = [],
            HasInternet = true,
        }));

        var result = await scanner.ScanAsync();

        Assert.Equal(ScanGroup.Missing, result.Group);
        var finding = Assert.Single(result.Findings, f => f.Id == "util-pc-LenovoVantage");
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.NotNull(finding.Data);
        Assert.Equal("winget-install", finding.Data!["kind"]);
        Assert.Equal("--id 9WZDNCRFJ4MV --source msstore", finding.Data!["winget"]);
        Assert.Equal("Для твоего компьютера", finding.Data!["section"]);
    }

    [Fact]
    public async Task ScanAsync_PcUtilityInstalled_ReportsOkWithReinstall()
    {
        var scanner = new UtilitiesScanner(new FakeUtilitiesProbe(new UtilitiesSnapshot
        {
            Manufacturer = "Dell Inc.",
            Model = "XPS 15",
            InstalledPrograms = ["Dell Command | Update"],
            PeripheralVendors = [],
            HasInternet = true,
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings, f => f.Id == "util-pc-DellCommandUpdate");
        Assert.Equal(Severity.Ok, finding.Severity);
        Assert.NotNull(finding.Data);
        // Установленная утилита: кнопка «Переустановить» через winget (kind + флаг reinstall).
        Assert.Equal("winget-install", finding.Data!.GetValueOrDefault("kind"));
        Assert.Equal("1", finding.Data.GetValueOrDefault("reinstall"));
    }

    [Fact]
    public async Task ScanAsync_PeripheralVendor_SuggestsDeviceUtility()
    {
        var scanner = new UtilitiesScanner(new FakeUtilitiesProbe(new UtilitiesSnapshot
        {
            Manufacturer = "Unknown OEM",
            Model = "Custom",
            InstalledPrograms = [],
            PeripheralVendors = ["Logitech USB Receiver"],
            HasInternet = true,
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings, f => f.Id == "util-dev-LogitechGHUB");
        Assert.Equal("Для подключённых устройств", finding.Data!["section"]);
        Assert.Equal("--id Logitech.GHUB", finding.Data!["winget"]);
    }

    [Fact]
    public async Task ScanAsync_NoInternet_WarnsAndSkipsDownloads()
    {
        var scanner = new UtilitiesScanner(new FakeUtilitiesProbe(new UtilitiesSnapshot
        {
            Manufacturer = "ASUS",
            Model = "ROG",
            InstalledPrograms = [],
            PeripheralVendors = [],
            HasInternet = false,
        }));

        var result = await scanner.ScanAsync();

        Assert.Contains(result.Findings, f => f.Id == "util-no-internet" && f.Severity == Severity.Warning);
    }

    [Fact]
    public async Task ScanAsync_NothingMatches_ReportsNoUtilitiesNeeded()
    {
        var scanner = new UtilitiesScanner(new FakeUtilitiesProbe(new UtilitiesSnapshot
        {
            Manufacturer = "Generic Computer",
            Model = "Box",
            InstalledPrograms = [],
            PeripheralVendors = [],
            HasInternet = true,
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("util-none", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    private sealed class FakeUtilitiesProbe(UtilitiesSnapshot snapshot) : IUtilitiesProbe
    {
        public Task<UtilitiesSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
