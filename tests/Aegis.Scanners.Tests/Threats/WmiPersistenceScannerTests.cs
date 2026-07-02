using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Threats;
using Xunit;

namespace Aegis.Scanners.Tests.Threats;

public sealed class WmiPersistenceScannerTests
{
    [Fact]
    public async Task ScanAsync_MaliciousCommand_IsDanger()
    {
        var scanner = new WmiPersistenceScanner(new FakeProbe(
        [
            new WmiPersistence { Name = "Updater", Kind = "командная строка", Action = "powershell -nop -w hidden -enc SQBFAFgA" },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(ScanGroup.Threats, finding.Group);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("Скрытый автозапуск через WMI", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_PlainConsumer_IsWarningNotDanger()
    {
        // Подписка без явных признаков вредоноса — «Внимание» (редкий механизм, проверь), не «Опасно».
        var scanner = new WmiPersistenceScanner(new FakeProbe(
        [
            new WmiPersistence { Name = "MonitorAgent", Kind = "скрипт", Action = "Log-Event \"heartbeat\"" },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_IexploreInAction_IsNotDanger()
    {
        // «iex» — часть iexplore.exe (Internet Explorer / режим IE в Edge), а НЕ Invoke-Expression:
        // легитимный запуск не должен получать страшное «Опасно» (регресс аудита 2026-07-02).
        var scanner = new WmiPersistenceScanner(new FakeProbe(
        [
            new WmiPersistence
            {
                Name = "IeLauncher",
                Kind = "командная строка",
                Action = @"C:\Program Files\Internet Explorer\iexplore.exe https://intranet.local",
            },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_StandaloneIex_IsDanger()
    {
        // «iex» отдельным словом = Invoke-Expression — реальный признак вредоноса.
        var scanner = new WmiPersistenceScanner(new FakeProbe(
        [
            new WmiPersistence { Name = "Loader", Kind = "скрипт", Action = "iex $decoded" },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_KnownGoodConsumer_IsSkipped()
    {
        var scanner = new WmiPersistenceScanner(new FakeProbe(
        [
            new WmiPersistence { Name = "SCM Event Provider", Kind = "командная строка", Action = "..." },
        ]));

        Assert.Empty((await scanner.ScanAsync()).Findings);
    }

    private sealed class FakeProbe(IReadOnlyList<WmiPersistence> items) : IWmiPersistenceProbe
    {
        public Task<IReadOnlyList<WmiPersistence>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(items);
    }
}
