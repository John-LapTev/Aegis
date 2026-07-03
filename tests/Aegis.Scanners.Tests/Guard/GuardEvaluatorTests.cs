using Aegis.Scanners.Guard;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Guard;

public sealed class GuardEvaluatorTests
{
    private readonly GuardEvaluator _evaluator = new();

    [Fact]
    public void UnsignedHighCpu_UserAway_RaisesAlert()
    {
        var alerts = _evaluator.Evaluate(
            [Proc("tool.exe", cpu: 60, sig: SignatureStatus.Unsigned, path: @"C:\Program Files\App\tool.exe")],
            idle: TimeSpan.FromMinutes(15));

        var alert = Assert.Single(alerts);
        Assert.Contains("пока вас нет", alert.Message);
    }

    [Fact]
    public void UnsignedHighCpu_UserActive_NormalPath_NoAlert()
    {
        // Человек за компьютером, обычная папка, имя нормальное — в фоне не трогаем (сильного признака нет).
        var alerts = _evaluator.Evaluate(
            [Proc("tool.exe", cpu: 60, sig: SignatureStatus.Unsigned, path: @"C:\Program Files\App\tool.exe")],
            idle: TimeSpan.Zero);

        Assert.Empty(alerts);
    }

    [Fact]
    public void UnsignedHighCpu_StealthPath_RaisesAlertEvenWhenActive()
    {
        var alerts = _evaluator.Evaluate(
            [Proc("svc.exe", cpu: 60, sig: SignatureStatus.Unsigned, path: @"C:\Users\Bob\AppData\Roaming\svc.exe")],
            idle: TimeSpan.Zero);

        Assert.Single(alerts);
    }

    [Fact]
    public void SignedHighCpu_NoAlert()
    {
        var alerts = _evaluator.Evaluate(
            [Proc("game.exe", cpu: 95, sig: SignatureStatus.Signed, path: @"C:\Games\game.exe")],
            idle: TimeSpan.FromMinutes(30));

        Assert.Empty(alerts);
    }

    [Fact]
    public void LowCpu_NoAlert()
    {
        var alerts = _evaluator.Evaluate(
            [Proc("svc.exe", cpu: 5, sig: SignatureStatus.Unsigned, path: @"C:\Users\Bob\AppData\Roaming\svc.exe")],
            idle: TimeSpan.FromMinutes(30));

        Assert.Empty(alerts);
    }

    [Fact]
    public void SameFile_TwiceInList_AlertedOnce()
    {
        var alerts = _evaluator.Evaluate(
        [
            Proc("svc.exe", cpu: 60, sig: SignatureStatus.Unsigned, path: @"C:\Users\Bob\AppData\Roaming\svc.exe"),
            Proc("svc.exe", cpu: 55, sig: SignatureStatus.Unsigned, path: @"C:\Users\Bob\AppData\Roaming\svc.exe"),
        ], idle: TimeSpan.Zero);

        Assert.Single(alerts);
    }

    private static ProcessInfo Proc(string name, double cpu, SignatureStatus sig, string path) => new()
    {
        ProcessId = 100,
        Name = name,
        ExecutablePath = path,
        Signature = sig,
        CpuPercent = cpu,
    };
}
