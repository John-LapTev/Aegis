using Microsoft.Win32;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Fixing;
using Xunit;

namespace Aegis.System.Tests.Fixing;

/// <summary>
/// Сборка списка правок — чистая логика (сам доступ к реестру Windows-only и здесь не выполняется).
/// Проверяем главное обещание: правка снимает политику, которая иначе перебила бы настройку, и делает это
/// в том же бэкапе (значит, откат вернёт и политику).
/// </summary>
public sealed class RegistryValuesFixTests
{
    private const string FirewallStandard =
        @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile";

    [Fact]
    public void FirewallEnable_AlsoClearsBlockingPolicy()
    {
        var fix = new RegistryValuesFix(
            new RegistryBackupStore(),
            "settings-firewall-off",
            ScanGroup.Settings,
            [new RegistryValueEdit(RegistryHive.LocalMachine, FirewallStandard, "EnableFirewall", 1)],
            "тест");

        // Первая правка — сама настройка; следом снятие политики WindowsFirewall (Value=null → удалить).
        Assert.Equal(2, fix.Edits.Count);
        Assert.Equal(1, fix.Edits[0].Value);

        var policy = fix.Edits[1];
        Assert.Equal(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\StandardProfile", policy.SubKey);
        Assert.Equal("EnableFirewall", policy.ValueName);
        Assert.Null(policy.Value);
    }

    [Fact]
    public void SettingWithoutKnownPolicy_LeavesEditsUntouched()
    {
        var fix = new RegistryValuesFix(
            new RegistryBackupStore(),
            "some-finding",
            ScanGroup.Settings,
            [new RegistryValueEdit(RegistryHive.CurrentUser, @"SOFTWARE\Aegis\Test", "Value", 1)],
            "тест");

        Assert.Single(fix.Edits);
    }

    [Fact]
    public void MultipleProfiles_EachGetsItsOwnPolicyCleared_WithoutDuplicates()
    {
        var fix = new RegistryValuesFix(
            new RegistryBackupStore(),
            "settings-firewall-off",
            ScanGroup.Settings,
            [
                new RegistryValueEdit(RegistryHive.LocalMachine, FirewallStandard, "EnableFirewall", 1),
                new RegistryValueEdit(RegistryHive.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile",
                    "EnableFirewall", 1),
            ],
            "тест");

        // 2 настройки + 2 разные политики, без повторов.
        Assert.Equal(4, fix.Edits.Count);
        Assert.Equal(4, fix.Edits.Select(e => $"{e.Hive}|{e.SubKey}|{e.ValueName}").Distinct().Count());
        Assert.Equal(2, fix.Edits.Count(e => e.Value is null));
    }

    [Fact]
    public void EmptyEdits_Rejected()
    {
        Assert.Throws<ArgumentException>(() => new RegistryValuesFix(
            new RegistryBackupStore(), "id", ScanGroup.Settings, [], "тест"));
    }
}
