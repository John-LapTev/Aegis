using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Settings;
using Xunit;

namespace Aegis.Scanners.Tests.Settings;

/// <summary>
/// Игровые твики предлагаются только там, где они действительно работают. Твик, предложенный «вслепую»,
/// в лучшем случае бесполезен, в худшем — ухудшает работу; поэтому гейты проверяем отдельно.
/// </summary>
public sealed class GameTweaksScannerTests
{
    [Fact]
    public void HardwareScheduling_OfferedForDiscreteGpuOnModernDriver()
    {
        Assert.True(GameTweaksScanner.ShouldOfferHardwareScheduling(Ready()));
    }

    [Fact]
    public void HardwareScheduling_NotOfferedForIntegratedGpu()
    {
        Assert.False(GameTweaksScanner.ShouldOfferHardwareScheduling(Ready() with { HasDiscreteGpu = false }));
    }

    [Fact]
    public void HardwareScheduling_NotOfferedOnVirtualMachine()
    {
        Assert.False(GameTweaksScanner.ShouldOfferHardwareScheduling(Ready() with { IsVirtualMachine = true }));
    }

    [Fact]
    public void HardwareScheduling_NotOfferedOnOldDriverModel()
    {
        // WDDM 2.6 — функции просто нет, значение в реестре ничего не изменит.
        Assert.False(GameTweaksScanner.ShouldOfferHardwareScheduling(Ready() with { WddmVersion = 2600 }));
    }

    [Fact]
    public void HardwareScheduling_NotOfferedWhenAlreadyEnabled()
    {
        Assert.False(GameTweaksScanner.ShouldOfferHardwareScheduling(Ready() with { HardwareSchedulingEnabled = true }));
    }

    [Fact]
    public async Task Scan_ReadySystem_NoFindings()
    {
        var findings = await Scan(new GameReadiness
        {
            HardwareSchedulingEnabled = true,
            HasDiscreteGpu = true,
            WddmVersion = 3000,
            HasVisualCppX64 = true,
            HasVisualCppX86 = true,
            HasDirectXRuntime = true,
        });

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Scan_MissingRuntimes_OffersInstall()
    {
        var findings = await Scan(new GameReadiness { HasDirectXRuntime = true });

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(FindingKinds.WingetInstall, f.Data![FindingDataKeys.Kind]));
        Assert.Contains(findings, f => f.Data!["winget"].Contains("VCRedist") && f.Data["winget"].Contains("x64"));
    }

    [Fact]
    public async Task Scan_HardwareSchedulingFinding_CarriesRegistryCoordinates()
    {
        var findings = await Scan(Ready() with { HasVisualCppX64 = true, HasVisualCppX86 = true, HasDirectXRuntime = true });

        var hags = Assert.Single(findings);
        Assert.Equal("game-hags", hags.Id);
        Assert.Equal(FindingKinds.RegistryToggle, hags.Data![FindingDataKeys.Kind]);
        Assert.Equal("HwSchMode", hags.Data[FindingDataKeys.Name]);
        Assert.Equal("2", hags.Data["value"]);
    }

    private static GameReadiness Ready() => new()
    {
        HardwareSchedulingEnabled = false,
        HasDiscreteGpu = true,
        WddmVersion = 2700,
        IsVirtualMachine = false,
    };

    private static async Task<IReadOnlyList<Finding>> Scan(GameReadiness readiness)
    {
        var scanner = new GameTweaksScanner(new FakeProbe(readiness));
        return (await scanner.ScanAsync()).Findings;
    }

    private sealed class FakeProbe(GameReadiness readiness) : IGameReadinessProbe
    {
        public Task<GameReadiness> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(readiness);
    }
}
