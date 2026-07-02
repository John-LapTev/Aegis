using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Registry;
using Xunit;

namespace Aegis.Scanners.Tests.Registry;

public sealed class RegistryScannerTests
{
    [Fact]
    public async Task ScanAsync_OrphanedUninstallEntry_IsWarning()
    {
        var scanner = new RegistryScanner(new FakeRegistryProbe(
        [
            new RegistryIssue
            {
                Path = @"HKLM\SOFTWARE\...\Uninstall\{old-app}",
                Kind = RegistryIssueKind.OrphanedUninstallEntry,
            },
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.StartsWith("registry-OrphanedUninstallEntry-", finding.Id);
    }

    [Fact]
    public async Task ScanAsync_EmptyAutostartKey_IsInfo()
    {
        var scanner = new RegistryScanner(new FakeRegistryProbe(
        [
            new RegistryIssue { Path = @"HKCU\...\Run\Empty", Kind = RegistryIssueKind.EmptyAutostartKey },
        ]));

        var result = await scanner.ScanAsync();

        Assert.Equal(Severity.Info, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public async Task ScanAsync_IncludesReferenceInDetail_WhenPresent()
    {
        var scanner = new RegistryScanner(new FakeRegistryProbe(
        [
            new RegistryIssue
            {
                Path = @"HKCU\...\Run\Ghost",
                Kind = RegistryIssueKind.InvalidStartupReference,
                Reference = @"C:\gone\ghost.exe",
            },
        ]));

        var result = await scanner.ScanAsync();

        Assert.Contains(@"C:\gone\ghost.exe", Assert.Single(result.Findings).Detail);
    }

    [Fact]
    public async Task ScanAsync_DistinctPaths_ProduceDistinctIds()
    {
        var scanner = new RegistryScanner(new FakeRegistryProbe(
        [
            new RegistryIssue { Path = @"HKLM\...\Uninstall\A", Kind = RegistryIssueKind.OrphanedUninstallEntry },
            new RegistryIssue { Path = @"HKLM\...\Uninstall\B", Kind = RegistryIssueKind.OrphanedUninstallEntry },
        ]));

        var result = await scanner.ScanAsync();

        Assert.Equal(2, result.Findings.Count);
        Assert.NotEqual(result.Findings[0].Id, result.Findings[1].Id);
    }

    private sealed class FakeRegistryProbe(IReadOnlyList<RegistryIssue> issues) : IRegistryProbe
    {
        public Task<IReadOnlyList<RegistryIssue>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(issues);
    }
}
