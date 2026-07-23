using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Fixing;
using Xunit;

namespace Aegis.System.Tests.Fixing;

public sealed class FixFactoryTests
{
    // Регрессионный тест: находка «медленная СЛУЖБА» из «Автозапуска» несёт Group=Autostart + kind=service-disable.
    // Раньше ветка Autostart в FixFactory ловила её первой → AutostartDisableFix падал «Неизвестный тип автозапуска».
    // Должна попадать в ветку ServiceDisable → обратимый RegistryValueFix (Services\<name>\Start=4). Аудит 2026-07-04.
    [Fact]
    public void ServiceDisableInAutostartGroup_RoutesToRegistryValueFix_NotAutostart()
    {
        var factory = Create();
        var finding = new Finding
        {
            Id = "boot-culprit-Service-SomeSvc",
            Group = ScanGroup.Autostart,
            Severity = Severity.Info,
            Title = "Служба «SomeSvc» дольше многих грузится при старте",
            Detail = "SomeSvc",
            Explain = "тест",
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.ServiceDisable,
                ["service"] = "SomeSvc",
            },
        };

        var fix = factory.CreateFix(finding);

        Assert.NotNull(fix);
        Assert.IsType<RegistryValueFix>(fix);
    }

    [Fact]
    public void AutostartRunFinding_RoutesToAutostartDisableFix()
    {
        var factory = Create();
        var finding = new Finding
        {
            Id = "autostart-run-Foo",
            Group = ScanGroup.Autostart,
            Severity = Severity.Info,
            Title = "Автозапуск: Foo",
            Detail = "Foo",
            Explain = "тест",
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.AutostartRun,
                ["hive"] = "HKCU",
                ["subkey"] = @"Software\Microsoft\Windows\CurrentVersion\Run",
                ["name"] = "Foo",
            },
        };

        var fix = factory.CreateFix(finding);

        Assert.IsType<AutostartDisableFix>(fix);
    }

    [Fact]
    public void FirewallFinding_RoutesToMultiValueFix()
    {
        // Брандмауэр может быть выключен в нескольких профилях сразу — правка должна быть групповой,
        // иначе включится только один профиль, а остальные останутся дырой (разбор Kudu, 2026-07-23).
        var factory = Create();
        var finding = new Finding
        {
            Id = "settings-firewall-off",
            Group = ScanGroup.Settings,
            Severity = Severity.Danger,
            Title = "Брандмауэр Windows выключен",
            Detail = "тест",
            Explain = "тест",
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.FirewallEnable,
                [FindingDataKeys.Profiles] = "StandardProfile,PublicProfile",
            },
        };

        var fix = factory.CreateFix(finding);

        Assert.IsType<RegistryValuesFix>(fix);
    }

    [Fact]
    public void FirewallFinding_WithoutProfiles_StillBuildsFix()
    {
        // Данных о профилях нет (старая находка) — правка всё равно должна собраться и включить всё.
        var factory = Create();
        var finding = new Finding
        {
            Id = "settings-firewall-off",
            Group = ScanGroup.Settings,
            Severity = Severity.Danger,
            Title = "Брандмауэр Windows выключен",
            Detail = "тест",
            Explain = "тест",
            Data = new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.FirewallEnable },
        };

        Assert.IsType<RegistryValuesFix>(factory.CreateFix(finding));
    }

    private static FixFactory Create() => new(
        new RegistryBackupStore(),
        new QuarantineStore(),
        new RegistryKeyBackupStore(),
        new ScheduledTaskBackupStore(),
        new AppxRemovalBackupStore(),
        new FakeDriverCatalog());

    private sealed class FakeDriverCatalog : IDriverUpdateCatalog
    {
        public Task<IReadOnlyList<DriverUpdateOffer>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DriverUpdateOffer>>([]);

        public Task<DriverInstallResult> InstallAsync(string updateId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DriverInstallResult.Failed("n/a"));
    }
}
