using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Settings;
using Xunit;

namespace Aegis.Scanners.Tests.Settings;

public sealed class SecurityPostureScannerTests
{
    [Fact]
    public async Task FreshUpdates_NoFinding()
    {
        var findings = await Scan(new SecurityPosture { DaysSinceLastUpdate = 10 });

        Assert.DoesNotContain(findings, f => f.Id == "posture-updates-stale");
    }

    [Fact]
    public async Task OldUpdates_Warns()
    {
        var finding = Assert.Single(await Scan(new SecurityPosture { DaysSinceLastUpdate = 60 }));

        Assert.Equal("posture-updates-stale", finding.Id);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task VeryOldUpdates_IsDanger()
    {
        var finding = Assert.Single(await Scan(new SecurityPosture { DaysSinceLastUpdate = 200 }));

        Assert.Equal(Severity.Danger, finding.Severity);
    }

    [Fact]
    public async Task UnknownUpdateDate_SaysNothing()
    {
        // Дату узнать не удалось — молчим, а не пугаем «обновлений нет» (честность важнее заполненной плитки).
        Assert.Empty(await Scan(new SecurityPosture()));
    }

    [Fact]
    public async Task EncryptedDisk_IsOkTile()
    {
        var findings = await Scan(new SecurityPosture
        {
            Volumes = [new EncryptedVolume { Mount = "C:", Protected = true }],
        });

        var encryption = Assert.Single(findings, f => f.Id == "posture-encryption");
        Assert.Equal(Severity.Ok, encryption.Severity);
    }

    [Fact]
    public async Task UnencryptedDisk_IsAdviceNotAlarm()
    {
        // Незашифрованный домашний компьютер — не «проблема»: пугать человека здесь нечем, это совет.
        var findings = await Scan(new SecurityPosture
        {
            Volumes = [new EncryptedVolume { Mount = "C:", Protected = false }],
        });

        var encryption = Assert.Single(findings, f => f.Id == "posture-encryption");
        Assert.Equal(Severity.Info, encryption.Severity);
        Assert.Contains("ключ восстановления", encryption.Explain);
    }

    [Fact]
    public async Task NoBitlockerSupport_NoTile()
    {
        Assert.DoesNotContain(await Scan(new SecurityPosture { Volumes = [] }), f => f.Id == "posture-encryption");
    }

    [Fact]
    public async Task CommonWindowsPorts_AreNotReported()
    {
        // 445/135/139 открыты почти на каждом ПК с Windows — сообщать о них значит пугать без причины.
        var findings = await Scan(new SecurityPosture { ListeningPorts = [135, 139, 445, 5040] });

        Assert.DoesNotContain(findings, f => f.Id == "posture-open-ports");
    }

    [Fact]
    public async Task UnusualPort_IsReportedAsAdvice()
    {
        var findings = await Scan(new SecurityPosture { ListeningPorts = [445, 3389] });

        var ports = Assert.Single(findings, f => f.Id == "posture-open-ports");
        Assert.Equal(Severity.Info, ports.Severity);
        Assert.Contains("3389", ports.Explain);
    }

    [Fact]
    public async Task HighDynamicPorts_AreIgnored()
    {
        // Порты 49152+ система раздаёт временно самим программам — это не «открытая дверь».
        Assert.DoesNotContain(await Scan(new SecurityPosture { ListeningPorts = [50123, 51999] }),
            f => f.Id == "posture-open-ports");
    }

    [Fact]
    public async Task NoLockOnResume_IsAdvice()
    {
        var findings = await Scan(new SecurityPosture { LockOnResume = false, WindowsHelloEnabled = true });

        var tile = Assert.Single(findings, f => f.Id == "posture-lock");
        Assert.Equal(Severity.Info, tile.Severity);
        Assert.Contains("ПИН-код", tile.Explain);
    }

    [Fact]
    public async Task UnknownLockState_NoTile()
    {
        Assert.DoesNotContain(await Scan(new SecurityPosture { LockOnResume = null }), f => f.Id == "posture-lock");
    }

    private static async Task<IReadOnlyList<Finding>> Scan(SecurityPosture posture)
    {
        var scanner = new SecurityPostureScanner(new FakeProbe(posture));
        return (await scanner.ScanAsync()).Findings;
    }

    private sealed class FakeProbe(SecurityPosture posture) : ISecurityPostureProbe
    {
        public Task<SecurityPosture> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(posture);
    }
}
