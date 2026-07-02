using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Processes;
using Xunit;

namespace Aegis.Scanners.Tests.Processes;

public sealed class ProcessesScannerTests
{
    [Fact]
    public async Task ScanAsync_WindowsProcesses_SummarizedAsSafe()
    {
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("svchost", @"C:\Windows\System32\svchost.exe", SignatureStatus.Signed, "Microsoft Corporation", cpu: 3),
            Process("explorer", @"C:\Windows\explorer.exe", SignatureStatus.Signed, "Microsoft Windows", cpu: 2),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Ok, finding.Severity);
        Assert.Contains("процессы Windows", finding.Title);
        Assert.Contains("2", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_HardwareVendor_IsSafeWithOriginLabel()
    {
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("nvcontainer", @"C:\Program Files\NVIDIA Corporation\nvcontainer.exe",
                SignatureStatus.Signed, "NVIDIA Corporation", cpu: 5),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Ok, finding.Severity);
        Assert.Equal("Видеокарта (NVIDIA)", finding.Data!["category"]);
    }

    [Fact]
    public async Task ScanAsync_UnsignedFromTempFolder_IsDanger()
    {
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("xmr", @"C:\Users\Ivan\AppData\Local\Temp\xmr.exe", SignatureStatus.Unsigned, null, cpu: 4),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("временной папки", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_HighCpuWithoutSignature_IsFlaggedAsPossibleMiner()
    {
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("worker", @"C:\ProgramData\worker\worker.exe", SignatureStatus.Unsigned, null, cpu: 95),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("майнер", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_UnsignedNormalLowLoad_IsWarning()
    {
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("tool", @"C:\Apps\tool\tool.exe", SignatureStatus.Unsigned, null, cpu: 8),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_SignedThirdParty_SummarizedAsSafe_NotAlarming()
    {
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("steam", @"C:\Program Files (x86)\Steam\steam.exe", SignatureStatus.Signed, "Valve Corp.", cpu: 12),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Ok, finding.Severity);
        Assert.Contains("подписью", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_SameNameDifferentPath_ProducesDistinctIds()
    {
        // Одно имя, но РАЗНЫЕ файлы — это два разных процесса, показываем оба с уникальными Id.
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("svc", @"C:\Users\Ivan\AppData\Local\Temp\a\svc.exe", SignatureStatus.Unsigned, null, cpu: 4, pid: 1111),
            Process("svc", @"C:\Users\Ivan\AppData\Local\Temp\b\svc.exe", SignatureStatus.Unsigned, null, cpu: 4, pid: 2222),
        ]));

        var result = await scanner.ScanAsync();

        Assert.Equal(2, result.Findings.Count);
        Assert.NotEqual(result.Findings[0].Id, result.Findings[1].Id);
    }

    [Fact]
    public async Task ScanAsync_SameFileMultipleInstances_DeduplicatedToOne()
    {
        // Один и тот же файл, запущенный несколькими копиями (напр. NVDisplay.Container ×2) — одна строка.
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("svc", @"C:\Users\Ivan\AppData\Local\Temp\svc.exe", SignatureStatus.Unsigned, null, cpu: 4, pid: 1111),
            Process("svc", @"C:\Users\Ivan\AppData\Local\Temp\svc.exe", SignatureStatus.Unsigned, null, cpu: 4, pid: 2222),
        ]));

        var result = await scanner.ScanAsync();

        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task ScanAsync_ProtectedSystemProcess_NotFlaggedAsUnsigned()
    {
        // Защищённый системный файл, подпись которого не прочиталась (выглядит «без подписи»),
        // не должен пугать пользователя — это процесс самой Windows, уходит в сводку.
        var scanner = new ProcessesScanner(new FakeProcessProbe(
        [
            Process("winlogon", @"C:\WINDOWS\system32\winlogon.exe", SignatureStatus.Unsigned, null, cpu: 1),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Ok, finding.Severity);
        Assert.Contains("Windows", finding.Title);
    }

    private static ProcessInfo Process(string name, string path, SignatureStatus signature, string? publisher, double cpu, int pid = 1000) =>
        new()
        {
            ProcessId = pid,
            Name = name,
            ExecutablePath = path,
            Signature = signature,
            Publisher = publisher,
            CpuPercent = cpu,
        };

    private sealed class FakeProcessProbe(IReadOnlyList<ProcessInfo> processes) : IProcessProbe
    {
        public Task<IReadOnlyList<ProcessInfo>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(processes);
    }
}
