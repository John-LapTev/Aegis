using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Settings;
using Xunit;

namespace Aegis.Scanners.Tests.Settings;

public sealed class PolicyScannerTests
{
    [Fact]
    public async Task DisabledDefender_IsDangerWithClearAction()
    {
        var scanner = new PolicyScanner(new FakePolicyProbe([
            new PolicyRestriction
            {
                Hive = "HKLM",
                SubKey = @"SOFTWARE\Policies\Microsoft\Windows Defender",
                ValueName = "DisableAntiSpyware",
                Value = 1,
            },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Equal(FindingKinds.PolicyClear, finding.Data![FindingDataKeys.Kind]);
        Assert.Equal("DisableAntiSpyware", finding.Data[FindingDataKeys.Name]);
        Assert.Contains("Защитник", finding.Title);
    }

    [Fact]
    public async Task UnknownRestriction_IsIgnored()
    {
        // Пробник вернул то, чего нет в каталоге, — показывать нечего: объяснить это человеку мы не сможем,
        // а починка по неизвестным координатам удалила бы произвольное значение реестра.
        var scanner = new PolicyScanner(new FakePolicyProbe([
            new PolicyRestriction
            {
                Hive = "HKLM",
                SubKey = @"SOFTWARE\Что-то\Своё",
                ValueName = "Неизвестно",
                Value = 1,
            },
        ]));

        Assert.Empty((await scanner.ScanAsync()).Findings);
    }

    [Fact]
    public async Task CleanSystem_NoFindings()
    {
        var scanner = new PolicyScanner(new FakePolicyProbe([]));

        Assert.Empty((await scanner.ScanAsync()).Findings);
    }

    [Fact]
    public void IsBad_FixedValue_MatchesOnlyThatValue()
    {
        var rule = PolicyCatalog.Rules.First(r => r.ValueName == "DisableTaskMgr");

        Assert.True(PolicyCatalog.IsBad(rule, 1));
        Assert.False(PolicyCatalog.IsBad(rule, 0));
        Assert.False(PolicyCatalog.IsBad(rule, null));
    }

    [Fact]
    public void IsBad_AnyNonZero_WhenNoFixedValue()
    {
        // NoDrives — это маска скрытых дисков: вредно любое ненулевое значение, а не какое-то конкретное.
        var rule = PolicyCatalog.Rules.First(r => r.ValueName == "NoDrives");

        Assert.True(PolicyCatalog.IsBad(rule, 4));
        Assert.True(PolicyCatalog.IsBad(rule, 67108863));
        Assert.False(PolicyCatalog.IsBad(rule, 0));
    }

    [Fact]
    public void EnableLuaRule_TriggersOnZero_NotOne()
    {
        // Здесь «вредно» — ноль (UAC выключен), а единица наоборот нормальна: правило хранит BadValue=0.
        var rule = PolicyCatalog.Rules.First(r => r.ValueName == "EnableLUA");

        Assert.True(PolicyCatalog.IsBad(rule, 0));
        Assert.False(PolicyCatalog.IsBad(rule, 1));
    }

    [Fact]
    public void EveryRuleHasHumanText()
    {
        foreach (var rule in PolicyCatalog.Rules)
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Title));
            // Объяснение должно быть развёрнутым: человек не знает, что такое «политика реестра».
            Assert.True(rule.Explain.Length > 60, $"Короткое объяснение у {rule.ValueName}");
        }
    }

    private sealed class FakePolicyProbe(IReadOnlyList<PolicyRestriction> restrictions) : IPolicyProbe
    {
        public Task<IReadOnlyList<PolicyRestriction>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(restrictions);
    }
}
