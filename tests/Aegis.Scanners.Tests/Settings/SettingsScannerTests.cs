using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Settings;
using Xunit;

namespace Aegis.Scanners.Tests.Settings;

public sealed class SettingsScannerTests
{
    [Fact]
    public async Task ScanAsync_FirewallOff_IsDanger()
    {
        var scanner = new SettingsScanner(new FakeSettingsProbe(Secure() with { FirewallEnabled = false }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("settings-firewall-off", finding.Id);
        Assert.Equal(Severity.Danger, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_FirewallOffInPublicOnly_NamesProfileAndPassesItToFix()
    {
        // Выключенный публичный профиль — самая опасная дыра (Wi-Fi в кафе), и раньше он вообще не проверялся:
        // читался только частный профиль (найдено при разборе Kudu, 2026-07-23).
        var scanner = new SettingsScanner(new FakeSettingsProbe(Secure() with
        {
            FirewallEnabled = false,
            DisabledFirewallProfiles = ["PublicProfile"],
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("settings-firewall-off", finding.Id);
        Assert.Contains("общественная сеть", finding.Detail);
        Assert.Equal(FindingKinds.FirewallEnable, finding.Data![FindingDataKeys.Kind]);
        Assert.Equal("PublicProfile", finding.Data[FindingDataKeys.Profiles]);
    }

    [Fact]
    public async Task ScanAsync_FirewallOffEverywhere_ListsAllProfiles()
    {
        var scanner = new SettingsScanner(new FakeSettingsProbe(Secure() with
        {
            FirewallEnabled = false,
            DisabledFirewallProfiles = ["DomainProfile", "StandardProfile", "PublicProfile"],
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("DomainProfile,StandardProfile,PublicProfile", finding.Data![FindingDataKeys.Profiles]);
    }

    [Fact]
    public async Task ScanAsync_UacOff_IsWarning()
    {
        var scanner = new SettingsScanner(new FakeSettingsProbe(Secure() with { UacEnabled = false }));

        var result = await scanner.ScanAsync();

        Assert.Equal(Severity.Warning, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public async Task ScanAsync_RemoteDesktopOn_IsInfo()
    {
        var scanner = new SettingsScanner(new FakeSettingsProbe(Secure() with { RemoteDesktopEnabled = true }));

        var result = await scanner.ScanAsync();

        Assert.Equal(Severity.Info, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public async Task ScanAsync_AllSecure_ReturnsNoFindings()
    {
        var scanner = new SettingsScanner(new FakeSettingsProbe(Secure()));

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task ScanAsync_MultipleProblems_ReturnsAll()
    {
        var scanner = new SettingsScanner(new FakeSettingsProbe(new SystemSettingsSnapshot
        {
            FirewallEnabled = false,
            UacEnabled = false,
            AutomaticUpdatesEnabled = false,
            RemoteDesktopEnabled = true,
        }));

        var result = await scanner.ScanAsync();

        Assert.Equal(4, result.Findings.Count);
    }

    private static SystemSettingsSnapshot Secure() => new()
    {
        FirewallEnabled = true,
        UacEnabled = true,
        AutomaticUpdatesEnabled = true,
        RemoteDesktopEnabled = false,
    };

    private sealed class FakeSettingsProbe(SystemSettingsSnapshot snapshot) : ISettingsProbe
    {
        public Task<SystemSettingsSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
