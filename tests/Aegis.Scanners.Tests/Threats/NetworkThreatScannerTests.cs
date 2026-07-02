using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Threats;
using Xunit;

namespace Aegis.Scanners.Tests.Threats;

public sealed class NetworkThreatScannerTests
{
    [Fact]
    public async Task ScanAsync_HostsBlackholeOfSecurityDomain_IsDanger()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            hosts: [new HostsEntry { Hostname = "windowsupdate.microsoft.com", MappedIp = "0.0.0.0" }])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("Заблокирована", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_HostsRedirectOfBankDomain_IsDanger()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            hosts: [new HostsEntry { Hostname = "online.sberbank.ru", MappedIp = "45.11.22.33" }])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("Подмена", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_HostsBlackholeOfBankDomain_IsDanger()
    {
        // Вредонос блокирует доступ к банку (домен → 0.0.0.0) — раньше падало в безобидный Info.
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            hosts: [new HostsEntry { Hostname = "online.sberbank.ru", MappedIp = "0.0.0.0" }])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("важному сайту", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_BenignCustomHostsEntry_IsInfo()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            hosts: [new HostsEntry { Hostname = "myserver.local", MappedIp = "192.168.1.50" }])));

        var result = await scanner.ScanAsync();

        Assert.Equal(Severity.Info, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public async Task ScanAsync_OnlyUnknownPublicDnsIsFlagged()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            dns: ["192.168.1.1", "8.8.8.8", "77.88.8.8", "45.55.55.55"])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("45.55.55.55", finding.Detail);
    }

    [Fact]
    public async Task ScanAsync_SuspiciousConnection_IsDanger()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            conns: [new SuspiciousConnection
            {
                ProcessName = "svc",
                RemoteAddress = "185.10.20.30",
                RemotePort = 3333,
                Reason = "известный пул майнинга",
            }])));

        var result = await scanner.ScanAsync();

        Assert.Equal(Severity.Danger, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public async Task ScanAsync_DistinctHostsEntries_ProduceDistinctIds()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            hosts:
            [
                new HostsEntry { Hostname = "a.example", MappedIp = "10.0.0.1" },
                new HostsEntry { Hostname = "b.example", MappedIp = "10.0.0.2" },
            ])));

        var result = await scanner.ScanAsync();

        Assert.Equal(2, result.Findings.Count);
        Assert.NotEqual(result.Findings[0].Id, result.Findings[1].Id);
    }

    [Fact]
    public async Task ScanAsync_BlockedActivationServers_IsWarning()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            hosts: [new HostsEntry { Hostname = "practivate.adobe.com", MappedIp = "0.0.0.0" }])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("активации", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_OrdinaryDomainRedirectedToPublicIp_IsWarning()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            hosts: [new HostsEntry { Hostname = "news.example.com", MappedIp = "203.0.113.5" }])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("чужой сервер", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_BloatedHostsFile_SummarizesKeepsDangerSuppressesInfo()
    {
        var entries = Enumerable.Range(0, 1000)
            .Select(i => new HostsEntry { Hostname = $"ad{i}.example", MappedIp = "0.0.0.0" })
            .Append(new HostsEntry { Hostname = "windowsupdate.microsoft.com", MappedIp = "0.0.0.0" })
            .ToList();
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(hosts: entries)));

        var result = await scanner.ScanAsync();

        // 1000 «Info» подавлены; остаются опасная запись + сводка о раздувании.
        Assert.Equal(2, result.Findings.Count);
        Assert.Contains(result.Findings, f => f.Id == "threat-hosts-bloat");
        Assert.Contains(result.Findings, f => f.Severity == Severity.Danger);
    }

    [Fact]
    public async Task ScanAsync_ConnectionToMiningPoolPort_IsDanger()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            active: [new ActiveConnection { ProcessName = "svc", RemoteAddress = "185.20.30.40", RemotePort = 3333 }])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("майнинг-пул", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_MiningPool_WithPid_GivesStopAction()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            active: [new ActiveConnection { ProcessName = "miner", ProcessId = 4321, RemoteAddress = "185.20.30.40", RemotePort = 3333 }])));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        // Известен PID → реальное действие «Остановить» (через ProcessStopFix).
        Assert.Equal("process-stop", finding.Data!["kind"]);
        Assert.Equal("4321", finding.Data!["pid"]);
    }

    [Fact]
    public async Task ScanAsync_MiningPool_WithoutPid_NoAction()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            active: [new ActiveConnection { ProcessName = "svc", ProcessId = 0, RemoteAddress = "185.20.30.40", RemotePort = 3333 }])));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        // PID неизвестен → кнопки нет (не обещаем то, что не можем сделать), но предупреждение остаётся.
        Assert.Null(finding.Data);
        Assert.Equal(Severity.Danger, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_ConnectionWithUnknownProcess_ShowsFriendlyLabel()
    {
        // PID не сопоставлен (имя пустое) — в тексте не должно быть «висящей» стрелки без программы.
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            active: [new ActiveConnection { ProcessName = string.Empty, RemoteAddress = "185.20.30.40", RemotePort = 3333 }])));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Contains("неизвестная программа", finding.Detail);
        Assert.DoesNotContain("threat-port--", finding.Id); // ID не зависит от пустого имени
    }

    [Fact]
    public async Task ScanAsync_ConnectionOverTorPort_IsWarning()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            active: [new ActiveConnection { ProcessName = "app", RemoteAddress = "51.15.1.2", RemotePort = 9050 }])));

        var result = await scanner.ScanAsync();

        Assert.Equal(Severity.Warning, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public async Task ScanAsync_OrdinaryConnection_IsNotFlagged()
    {
        var scanner = new NetworkThreatScanner(new FakeProbe(Snap(
            active: [new ActiveConnection { ProcessName = "browser", RemoteAddress = "142.250.1.1", RemotePort = 443 }])));

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    private static NetworkThreatSnapshot Snap(
        IReadOnlyList<HostsEntry>? hosts = null,
        IReadOnlyList<string>? dns = null,
        IReadOnlyList<SuspiciousConnection>? conns = null,
        IReadOnlyList<ActiveConnection>? active = null) =>
        new()
        {
            HostsEntries = hosts ?? [],
            DnsServers = dns ?? [],
            SuspiciousConnections = conns ?? [],
            ActiveConnections = active ?? [],
        };

    private sealed class FakeProbe(NetworkThreatSnapshot snapshot) : INetworkThreatProbe
    {
        public Task<NetworkThreatSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
