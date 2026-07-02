using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core.Scanning;
using Xunit;

namespace Aegis.Core.Tests.Scanning;

public sealed class ScanOrchestratorTests
{
    [Fact]
    public async Task ScanAllAsync_AggregatesFindingsFromAllScanners()
    {
        var orchestrator = new ScanOrchestrator(
        [
            new FakeScanner(ScanGroup.Autostart, findingCount: 2),
            new FakeScanner(ScanGroup.Junk, findingCount: 3),
        ]);

        var results = await orchestrator.ScanAllAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].Findings.Count);
        Assert.Equal(3, results[1].Findings.Count);
    }

    [Fact]
    public async Task ScanAllAsync_ReportsProgressForEachGroupAndCompletion()
    {
        var orchestrator = new ScanOrchestrator(
        [
            new FakeScanner(ScanGroup.Autostart, findingCount: 1),
            new FakeScanner(ScanGroup.Junk, findingCount: 1),
        ]);

        var reports = new List<ScanProgress>();
        var sync = new object();
        var progress = new SynchronousProgress(p => { lock (sync) { reports.Add(p); } }); // отчёты идут из разных потоков

        await orchestrator.ScanAllAsync(progress);

        // Параллельный скан: по одному отчёту «сканер завершён» на каждую группу (с результатом) + 1 финальный.
        // Порядок завершения недетерминирован, поэтому проверяем множество, а не позиции.
        Assert.Equal(3, reports.Count);
        Assert.Equal(2, reports.Count(r => r.JustCompleted is not null));
        // Группа из одного сканера → GroupComplete=true в её отчёте.
        Assert.All(reports.Where(r => r.JustCompleted is not null), r => Assert.True(r.GroupComplete));
        var groups = reports.Where(r => r.JustCompleted is not null).Select(r => r.JustCompleted!.Group).ToHashSet();
        Assert.Contains(ScanGroup.Autostart, groups);
        Assert.Contains(ScanGroup.Junk, groups);
        // Последний отчёт — финальный.
        Assert.True(reports[^1].IsComplete);
        Assert.Equal(2, reports[^1].CompletedGroups);
        Assert.Equal(2, reports[^1].TotalGroups);
        Assert.Equal(2, reports[^1].FindingsSoFar);
        Assert.Equal(1.0, reports[^1].Fraction);
    }

    [Fact]
    public async Task ScanAllAsync_WhenScannerThrows_TurnsErrorIntoVisibleFinding()
    {
        var orchestrator = new ScanOrchestrator(
        [
            new ThrowingScanner(ScanGroup.Registry),
        ]);

        var results = await orchestrator.ScanAllAsync();

        var finding = Assert.Single(results[0].Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(ScanGroup.Registry, finding.Group);
    }

    [Fact]
    public async Task ScanAllAsync_WithNoScanners_ReturnsEmpty()
    {
        var orchestrator = new ScanOrchestrator([]);

        var results = await orchestrator.ScanAllAsync();

        Assert.Empty(results);
    }

    private sealed class FakeScanner(ScanGroup group, int findingCount) : IScanner
    {
        public ScanGroup Group => group;

        public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
        {
            var findings = Enumerable.Range(0, findingCount)
                .Select(i => new Finding
                {
                    Id = $"{group}-{i}",
                    Group = group,
                    Severity = Severity.Info,
                    Title = $"Находка {i}",
                    Explain = "Тестовая находка.",
                })
                .ToList();

            return Task.FromResult(new ScanResult { Group = group, Findings = findings });
        }
    }

    private sealed class ThrowingScanner(ScanGroup group) : IScanner
    {
        public ScanGroup Group => group;

        public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("сбой сканера");
    }

    private sealed class SynchronousProgress(Action<ScanProgress> onReport) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => onReport(value);
    }
}
