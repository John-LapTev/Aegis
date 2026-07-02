using Microsoft.Win32;
using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

/// <summary>
/// Регресс на баг: <c>reg.exe export</c> требует полное имя куста (<c>HKEY_LOCAL_MACHINE</c>),
/// а находки несут короткое (<c>HKLM</c>). Без нормализации бэкап ветки падал и удаление
/// осиротевших записей реестра тихо не работало. Чистая строковая логика — выполняется на любой ОС.
/// </summary>
public sealed class RegistryHiveNamesTests
{
    [Theory]
    [InlineData("HKLM", "HKEY_LOCAL_MACHINE")]
    [InlineData("hklm", "HKEY_LOCAL_MACHINE")]
    [InlineData("HKCU", "HKEY_CURRENT_USER")]
    [InlineData("HKCR", "HKEY_CLASSES_ROOT")]
    [InlineData("HKU", "HKEY_USERS")]
    [InlineData("HKCC", "HKEY_CURRENT_CONFIG")]
    public void ToFullName_MapsShortHiveToRegExeName(string shortName, string expected) =>
        Assert.Equal(expected, RegistryHiveNames.ToFullName(shortName));

    [Theory]
    [InlineData("HKEY_LOCAL_MACHINE")]
    [InlineData("HKEY_CURRENT_USER")]
    public void ToFullName_PassesFullNamesThrough(string fullName) =>
        Assert.Equal(fullName, RegistryHiveNames.ToFullName(fullName));

    [Theory]
    [InlineData("HKLM", RegistryHive.LocalMachine)]
    [InlineData("HKCU", RegistryHive.CurrentUser)]
    [InlineData("HKCR", RegistryHive.ClassesRoot)]
    public void ToHive_MapsShortHiveToRegistryHive(string shortName, RegistryHive expected) =>
        Assert.Equal(expected, RegistryHiveNames.ToHive(shortName));
}
