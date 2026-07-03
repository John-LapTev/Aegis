using Aegis.System.Fixing;
using Xunit;

namespace Aegis.System.Tests.Fixing;

public sealed class ProgramUninstallerTests
{
    [Fact]
    public void ParseCommand_QuotedPathWithArgs_SplitsCorrectly()
    {
        var (exe, args) = ProgramUninstaller.ParseCommand("\"C:\\Program Files\\App\\uninstall.exe\" /S /norestart");

        Assert.Equal(@"C:\Program Files\App\uninstall.exe", exe);
        Assert.Equal("/S /norestart", args);
    }

    [Fact]
    public void ParseCommand_MsiExec_SplitsExeAndArgs()
    {
        var (exe, args) = ProgramUninstaller.ParseCommand("MsiExec.exe /X{12345678-1234-1234-1234-1234567890AB}");

        Assert.Equal("MsiExec.exe", exe);
        Assert.Equal("/X{12345678-1234-1234-1234-1234567890AB}", args);
    }

    [Fact]
    public void ParseCommand_UnquotedPathWithSpaces_SplitsAtExe()
    {
        var (exe, args) = ProgramUninstaller.ParseCommand(@"C:\Program Files\App\uninst.exe /S");

        Assert.Equal(@"C:\Program Files\App\uninst.exe", exe); // не режем по первому пробелу в «Program Files»
        Assert.Equal("/S", args);
    }

    [Fact]
    public void ParseCommand_NoArgs_ReturnsEmptyArgs()
    {
        var (exe, args) = ProgramUninstaller.ParseCommand(@"C:\App\unins000.exe");

        Assert.Equal(@"C:\App\unins000.exe", exe);
        Assert.Equal(string.Empty, args);
    }

    [Fact]
    public void ToRegExePath_64Bit_KeepsPlainPath()
    {
        var path = ProgramUninstaller.ToRegExePath(@"HKLM|64|SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\App");

        Assert.Equal(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\App", path);
    }

    [Fact]
    public void ToRegExePath_32Bit_InsertsWow6432Node()
    {
        var path = ProgramUninstaller.ToRegExePath(@"HKLM|32|SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\App");

        Assert.Equal(@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\App", path);
    }

    [Fact]
    public void ToRegExePath_CurrentUser_KeepsPlainPath()
    {
        var path = ProgramUninstaller.ToRegExePath(@"HKCU|Default|SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\App");

        Assert.Equal(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\App", path);
    }
}
