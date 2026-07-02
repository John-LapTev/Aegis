using Aegis.Core.Models;
using Aegis.Scanners.Privacy;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Privacy;

public sealed class PrivacyDebloatScannerTests
{
    [Fact]
    public async Task ScanAsync_MinimalTelemetryAndNothingElse_ShowsOnlyTelemetryOk()
    {
        var scanner = new PrivacyDebloatScanner(new FakeProbe(Quiet()));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("privacy-telemetry-ok", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_TelemetryLevelUnknown_ShowsUnknownNotOk()
    {
        // null = уровень телеметрии НЕ прочитан (нет прав/ключа). Нельзя выдавать это за зелёное «всё хорошо» —
        // иначе соврём пользователю про приватность (регресс аудита 2026-07-02).
        var scanner = new PrivacyDebloatScanner(new FakeProbe(Quiet() with { TelemetryLevel = null }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("privacy-telemetry-unknown", finding.Id);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_FullTelemetry_IsInformationalAdvice()
    {
        var scanner = new PrivacyDebloatScanner(new FakeProbe(Quiet() with { TelemetryLevel = 3 }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("privacy-telemetry-full", finding.Id);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_EnabledGetAction_DisabledShownAsDone()
    {
        var scanner = new PrivacyDebloatScanner(new FakeProbe(Quiet() with
        {
            Toggles =
            [
                Toggle("privacy-cortana", enabled: true),
                Toggle("privacy-websearch", enabled: true),
                Toggle("privacy-location", enabled: false),
            ],
        }));

        var result = await scanner.ScanAsync();

        // Включённые → действие (registry-toggle); выключенные → зелёное «уже отключено» (правка 729).
        var actionable = result.Findings
            .Where(f => f.Data?.GetValueOrDefault("kind") == "registry-toggle")
            .ToList();
        Assert.Equal(2, actionable.Count);

        var done = Assert.Single(result.Findings, f => f.Id == "privacy-location");
        Assert.Equal(Severity.Ok, done.Severity);
        Assert.Equal("1", done.Data?.GetValueOrDefault("done"));
    }

    [Fact]
    public async Task ScanAsync_ServiceBloat_CarriesServiceDisableData()
    {
        var scanner = new PrivacyDebloatScanner(new FakeProbe(Quiet() with
        {
            DebloatItems =
            [
                new DebloatItem { Name = "Сетевая служба Xbox", Category = "служба Xbox", Enabled = true, ServiceName = "XboxNetApiSvc" },
            ],
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings, f => f.Id.StartsWith("debloat-", StringComparison.Ordinal));
        Assert.Equal("debloat-XboxNetApiSvc", finding.Id);
        Assert.Equal("service-disable", finding.Data!["kind"]);
    }

    private static PrivacySnapshot Quiet() => new()
    {
        TelemetryLevel = 1,
        Toggles = [],
        DebloatItems = [],
    };

    private static PrivacyToggle Toggle(string id, bool enabled) => new()
    {
        Id = id,
        Title = id,
        Detail = string.Empty,
        Explain = string.Empty,
        Hive = "HKCU",
        SubKey = "SOFTWARE\\Test",
        ValueName = "Value",
        DisableValue = 0,
        Enabled = enabled,
    };

    private sealed class FakeProbe(PrivacySnapshot snapshot) : IPrivacyProbe
    {
        public Task<PrivacySnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
