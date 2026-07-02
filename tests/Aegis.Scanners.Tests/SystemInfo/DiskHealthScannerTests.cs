using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class DiskHealthScannerTests
{
    [Fact]
    public async Task ScanAsync_GoodDrive_IsOkWithReassuringVerdict()
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth { Name = "C:", Level = SmartHealthLevel.Good, Model = "Samsung 980" },
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Ok, finding.Severity); // вердикт «всё хорошо» теперь показывает статус-бейдж
        Assert.Equal("Диск C:", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_WarningDrive_IsWarning()
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth { Name = "D:", Level = SmartHealthLevel.Warning, ReallocatedSectorCount = 12 },
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("Диск D:", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_CriticalDrive_IsDanger()
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth { Name = "C:", Level = SmartHealthLevel.Critical },
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Equal("Диск C:", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_ShowsEveryDrive_IncludingHealthyOnes()
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth { Name = "C:", Level = SmartHealthLevel.Good },
            new SmartDriveHealth { Name = "D:", Level = SmartHealthLevel.Critical },
        ]));

        var result = await scanner.ScanAsync();

        Assert.Equal(2, result.Findings.Count);
    }

    [Fact]
    public async Task ScanAsync_BuildsMetricsDetailFromKnownFields()
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth
            {
                Name = "C:",
                Level = SmartHealthLevel.Good,
                Model = "WD Blue",
                PercentLifeUsed = 7,
                TemperatureCelsius = 38,
            },
        ]));

        var result = await scanner.ScanAsync();

        var detail = Assert.Single(result.Findings).Detail;
        Assert.NotNull(detail);
        Assert.Contains("WD Blue", detail);
        Assert.Contains("износ 7%", detail);
        Assert.Contains("38 °C", detail);
    }

    [Theory]
    [InlineData(50, "ok")]
    [InlineData(85, "warning")]
    [InlineData(95, "danger")]
    public async Task ScanAsync_DiskFill_GoesToDataWithColorBySeverity(int fill, string expectedSeverity)
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth { Name = "C:", Level = SmartHealthLevel.Good, FillPercent = fill },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(fill.ToString(), finding.Data!["fillPercent"]);
        Assert.Equal(expectedSeverity, finding.Data!["fillSeverity"]);
    }

    [Fact]
    public async Task ScanAsync_NoFillPercent_NoFillData()
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth { Name = "C:", Level = SmartHealthLevel.Good, FillPercent = null },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.True(finding.Data is null || !finding.Data.ContainsKey("fillPercent"));
    }

    [Fact]
    public async Task ScanAsync_UnreadableDisk_ExplainsRawAndHasNoFill()
    {
        var scanner = new DiskHealthScanner(new FakeDiskHealthProbe(
        [
            new SmartDriveHealth { Name = "Диск 1", Level = SmartHealthLevel.Good, FilesystemUnreadable = true, FillPercent = null },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("1", finding.Data!["raw"]);                       // серый бейдж «RAW» в углу
        Assert.Contains("не распознаёт формат", finding.Explain);      // объяснение в «?»
        Assert.False(finding.Data.ContainsKey("fillPercent"));         // % не показываем
        Assert.DoesNotContain("RAW", finding.Detail ?? string.Empty);  // длинного текста на плитке НЕТ
    }

    private sealed class FakeDiskHealthProbe(IReadOnlyList<SmartDriveHealth> drives) : IDiskHealthProbe
    {
        public Task<IReadOnlyList<SmartDriveHealth>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(drives);
    }
}
