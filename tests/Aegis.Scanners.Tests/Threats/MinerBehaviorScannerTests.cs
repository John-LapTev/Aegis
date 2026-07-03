using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Threats;
using Xunit;

namespace Aegis.Scanners.Tests.Threats;

public sealed class MinerBehaviorScannerTests
{
    [Fact]
    public async Task SignedProcess_HighCpu_NotFlagged()
    {
        var scanner = Build(process: Proc("game.exe", cpu: 90, sig: SignatureStatus.Signed, path: @"C:\Games\game.exe"));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("miner-behavior-none", finding.Id); // подписанная тяжёлая программа — законно, не трогаем
    }

    [Fact]
    public async Task Unsigned_HighCpu_NormalLocation_NotPersistent_NotFlagged()
    {
        // Без подтверждающих признаков (только неподписанное + нагрузка) — это покажет вкладка «Процессы», здесь молчим.
        var scanner = Build(process: Proc("tool.exe", cpu: 40, sig: SignatureStatus.Unsigned, path: @"C:\Program Files\App\tool.exe"));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("miner-behavior-none", finding.Id);
    }

    [Fact]
    public async Task Unsigned_HighCpu_InAppData_IsWarning()
    {
        var scanner = Build(process: Proc("svc.exe", cpu: 40, sig: SignatureStatus.Unsigned,
            path: @"C:\Users\Bob\AppData\Roaming\svc.exe"));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.StartsWith("miner-behavior-svc.exe", finding.Id);
        Assert.Equal(Severity.Warning, finding.Severity); // один подтверждающий признак (скрытая папка)
    }

    [Fact]
    public async Task Unsigned_HighCpu_AppData_AndAutostart_IsDanger()
    {
        var scanner = Build(
            process: Proc("svc.exe", cpu: 60, sig: SignatureStatus.Unsigned, path: @"C:\Users\Bob\AppData\Roaming\svc.exe"),
            autostart: Auto("svc", @"C:\Users\Bob\AppData\Roaming\svc.exe"));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Danger, finding.Severity); // два признака: скрытая папка + автозапуск
        Assert.Equal(FindingKinds.ProcessStop, finding.Data?["kind"]); // есть действие «остановить»
    }

    [Fact]
    public async Task LowCpu_NotFlagged()
    {
        var scanner = Build(process: Proc("svc.exe", cpu: 5, sig: SignatureStatus.Unsigned,
            path: @"C:\Users\Bob\AppData\Roaming\svc.exe"));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("miner-behavior-none", finding.Id);
    }

    [Fact]
    public async Task UserAway_CountsAsSignal()
    {
        // Нормальная папка, не в автозапуске, но нагрузка идёт пока пользователь отошёл → один признак → Внимание.
        var scanner = Build(
            process: Proc("tool.exe", cpu: 40, sig: SignatureStatus.Unsigned, path: @"C:\Program Files\App\tool.exe"),
            idle: TimeSpan.FromMinutes(20));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.StartsWith("miner-behavior-tool.exe", finding.Id);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task RandomHexName_CountsAsSignal()
    {
        var scanner = Build(process: Proc("a1b2c3d4e5.exe", cpu: 40, sig: SignatureStatus.Unsigned,
            path: @"C:\Program Files\a1b2c3d4e5.exe"));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.StartsWith("miner-behavior-", finding.Id);
    }

    private static MinerBehaviorScanner Build(ProcessInfo process, AutostartEntry? autostart = null, TimeSpan idle = default) =>
        new(new FakeProcessProbe([process]),
            new FakeAutostartProbe(autostart is null ? [] : [autostart]),
            new FakeActivityProbe(idle));

    private static ProcessInfo Proc(string name, double cpu, SignatureStatus sig, string path) => new()
    {
        ProcessId = 1234,
        Name = name,
        ExecutablePath = path,
        Signature = sig,
        CpuPercent = cpu,
    };

    private static AutostartEntry Auto(string name, string command) => new()
    {
        Name = name,
        Command = command,
        Location = AutostartLocation.RegistryRun,
        Source = @"HKCU\...\Run",
        Signature = SignatureStatus.Unsigned,
    };

    private sealed class FakeProcessProbe(IReadOnlyList<ProcessInfo> items) : IProcessProbe
    {
        public Task<IReadOnlyList<ProcessInfo>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(items);
    }

    private sealed class FakeAutostartProbe(IReadOnlyList<AutostartEntry> items) : IAutostartProbe
    {
        public Task<IReadOnlyList<AutostartEntry>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(items);
    }

    private sealed class FakeActivityProbe(TimeSpan idle) : IUserActivityProbe
    {
        public TimeSpan GetIdleDuration() => idle;
    }
}
