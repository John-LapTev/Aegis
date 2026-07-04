using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class BootPerformanceScannerTests
{
    [Fact]
    public async Task NoData_ShowsFriendlyNotice()
    {
        var scanner = Scanner(new BootPerformance());

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("boot-no-data", finding.Id);
    }

    [Fact]
    public async Task FastBoot_IsOk()
    {
        var scanner = Scanner(new BootPerformance { BootDuration = TimeSpan.FromSeconds(28) });

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("boot-duration", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task SlowBoot_ShowsDurationInTitle()
    {
        var scanner = Scanner(new BootPerformance { BootDuration = TimeSpan.FromSeconds(140) });

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("boot-duration", finding.Id);
        Assert.Contains("2 мин", finding.Title); // 140 сек → «2 мин 20 сек»
    }

    [Fact]
    public async Task Culprit_InstalledButNotInAutostart_OffersDeleteCompletely()
    {
        var scanner = Scanner(new BootPerformance
        {
            BootDuration = TimeSpan.FromSeconds(60),
            Culprits = [new BootCulprit { Name = "Slacker", Impact = TimeSpan.FromSeconds(12), Kind = BootCulpritKind.Application }],
        }, installed: ["Slacker"]);

        var culprit = Assert.Single((await scanner.ScanAsync()).Findings, f => f.Id.StartsWith("boot-culprit-", StringComparison.Ordinal));
        Assert.Equal(Severity.Info, culprit.Severity); // это рейтинг, не тревога
        Assert.Contains("Slacker", culprit.Title);
        Assert.Equal("Slacker", culprit.Data?["exe"]); // установлена, но не в автозапуске → доступна «Удалить полностью»
    }

    [Fact]
    public async Task Culprit_AlreadyRemoved_NotInAutostartNorInstalled_IsHidden()
    {
        // Программа удалена (нет ни в автозапуске, ни среди установленных), но осталась в журнале загрузки Windows —
        // не показываем «мёртвую» запись, о которой ничего нельзя сделать (запрос Ивана 1328).
        var scanner = Scanner(new BootPerformance
        {
            BootDuration = TimeSpan.FromSeconds(60),
            Culprits = [new BootCulprit { Name = "Rave.exe", Impact = TimeSpan.FromSeconds(32), Kind = BootCulpritKind.Application }],
        }, installed: ["Brave"]); // Brave установлен, но это НЕ Rave — по границе слова не спутается

        var findings = (await scanner.ScanAsync()).Findings;
        Assert.DoesNotContain(findings, f => f.Id.StartsWith("boot-culprit-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Culprit_FoundInAutostart_GetsDisableAction()
    {
        var autostart = new AutostartEntry
        {
            Name = "Opera",
            Command = @"C:\Users\Bob\AppData\Local\Programs\Opera\opera.exe --startup",
            Location = AutostartLocation.RegistryRun,
            Source = @"HKCU\...\Run",
            Signature = SignatureStatus.Signed,
            FixData = new Dictionary<string, string> { ["kind"] = FindingKinds.AutostartRun, ["value"] = "Opera" },
        };
        var scanner = Scanner(new BootPerformance
        {
            BootDuration = TimeSpan.FromSeconds(60),
            Culprits = [new BootCulprit { Name = "opera.exe", Impact = TimeSpan.FromSeconds(24), Kind = BootCulpritKind.Application }],
        }, [autostart]);

        var culprit = Assert.Single((await scanner.ScanAsync()).Findings, f => f.Id.StartsWith("boot-culprit-", StringComparison.Ordinal));
        Assert.Equal(FindingKinds.AutostartRun, culprit.Data?["kind"]); // получил действие «Отключить» из автозапуска
    }

    [Fact]
    public async Task NonSystemService_GetsDisableServiceAction()
    {
        var scanner = Scanner(new BootPerformance
        {
            BootDuration = TimeSpan.FromSeconds(60),
            Culprits = [new BootCulprit { Name = "DSAService", Impact = TimeSpan.FromSeconds(14), Kind = BootCulpritKind.Service }],
        });

        var culprit = Assert.Single((await scanner.ScanAsync()).Findings, f => f.Id.StartsWith("boot-culprit-", StringComparison.Ordinal));
        Assert.Equal(FindingKinds.ServiceDisable, culprit.Data?["kind"]); // кнопка «Отключить службу»
        Assert.Equal("DSAService", culprit.Data?["service"]);
    }

    [Fact]
    public async Task CoreWindowsService_HasNoDisableButton()
    {
        var scanner = Scanner(new BootPerformance
        {
            BootDuration = TimeSpan.FromSeconds(60),
            Culprits = [new BootCulprit { Name = "SysMain", Impact = TimeSpan.FromSeconds(10), Kind = BootCulpritKind.Service }],
        });

        var culprit = Assert.Single((await scanner.ScanAsync()).Findings, f => f.Id.StartsWith("boot-culprit-", StringComparison.Ordinal));
        Assert.Null(culprit.Data?.GetValueOrDefault("kind")); // системную службу не предлагаем отключать
        Assert.Equal(Severity.Ok, culprit.Severity);
    }

    [Fact]
    public async Task WindowsComponent_ShownAsInfo_WithFriendlyName_NoDisableAdvice()
    {
        var scanner = Scanner(new BootPerformance
        {
            BootDuration = TimeSpan.FromSeconds(60),
            Culprits = [new BootCulprit { Name = "MsMpEng.exe", Impact = TimeSpan.FromSeconds(38), Kind = BootCulpritKind.Service }],
        });

        var culprit = Assert.Single((await scanner.ScanAsync()).Findings, f => f.Id.StartsWith("boot-culprit-", StringComparison.Ordinal));
        Assert.Equal(Severity.Ok, culprit.Severity); // системный компонент — это норма, «ОК»
        Assert.Contains("Защитник", culprit.Title);
        Assert.Null(culprit.Data?.GetValueOrDefault("kind")); // системный компонент → без кнопки отключения
        Assert.Equal("1", culprit.Data?.GetValueOrDefault("info")); // информационная → без квадратика/«Безопасно»
    }

    [Fact]
    public async Task TinyCulprit_IsIgnored()
    {
        var scanner = Scanner(new BootPerformance
        {
            BootDuration = TimeSpan.FromSeconds(30),
            Culprits = [new BootCulprit { Name = "Blip", Impact = TimeSpan.FromMilliseconds(500), Kind = BootCulpritKind.Application }],
        });

        var findings = (await scanner.ScanAsync()).Findings;
        Assert.DoesNotContain(findings, f => f.Id.StartsWith("boot-culprit-", StringComparison.Ordinal));
    }

    private static BootPerformanceScanner Scanner(BootPerformance boot, AutostartEntry[]? autostart = null, string[]? installed = null) =>
        new(new FakeBootProbe(boot), new FakeAutostartProbe(autostart ?? []), new FakeInstalledProgramsProbe(installed ?? []));

    private sealed class FakeBootProbe(BootPerformance boot) : IBootPerformanceProbe
    {
        public Task<BootPerformance> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(boot);
    }

    private sealed class FakeAutostartProbe(IReadOnlyList<AutostartEntry> entries) : IAutostartProbe
    {
        public Task<IReadOnlyList<AutostartEntry>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(entries);
    }

    private sealed class FakeInstalledProgramsProbe(string[] names) : IInstalledProgramsProbe
    {
        public Task<IReadOnlyList<InstalledProgram>> FindAsync(bool includeHidden = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InstalledProgram>>(
                names.Select(n => new InstalledProgram { Name = n, RegistryKeyPath = "HKLM|64|" + n }).ToArray());
    }
}
