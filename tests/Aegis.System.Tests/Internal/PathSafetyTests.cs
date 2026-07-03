using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

public sealed class PathSafetyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Program Files")]                 // папка первого уровня — целиком не удаляем
    [InlineData(@"C:\ProgramData")]
    [InlineData(@"C:\Users\Bob\AppData\Roaming")]     // контейнер Roaming
    [InlineData(@"C:\Users\Bob\AppData\Local")]
    [InlineData(@"C:\Users\Bob\AppData\Roaming\Microsoft")]  // общая папка вендора
    [InlineData(@"C:\Users\Bob\AppData\Local\Google")]
    [InlineData(@"C:\ProgramData\Microsoft")]
    [InlineData(@"C:\Users\Bob\Desktop")]
    public void UnsafePaths_AreRejected(string? path)
    {
        Assert.False(PathSafety.IsSafeToDeleteFolder(path));
    }

    [Theory]
    [InlineData(@"C:\Program Files\OperaGX")]
    [InlineData(@"C:\Users\Bob\AppData\Roaming\OperaGX")]
    [InlineData(@"C:\Users\Bob\AppData\Local\Discord")]
    [InlineData(@"D:\Games\SomeGame")]
    public void SpecificProgramFolders_AreAllowed(string path)
    {
        Assert.True(PathSafety.IsSafeToDeleteFolder(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"HKLM\SOFTWARE")]                     // сам SOFTWARE — не удаляем
    [InlineData(@"HKLM\SOFTWARE\Microsoft")]           // общая ветка вендора
    [InlineData(@"HKCU\SOFTWARE\Classes")]
    [InlineData(@"HKLM\SOFTWARE\WOW6432Node")]
    public void UnsafeRegistryKeys_AreRejected(string? regPath)
    {
        Assert.False(PathSafety.IsSafeRegistryKey(regPath));
    }

    [Theory]
    [InlineData(@"HKCU\SOFTWARE\OperaSoftware")]
    [InlineData(@"HKLM\SOFTWARE\WOW6432Node\Discord")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{ABC-123}")]
    public void SpecificRegistryKeys_AreAllowed(string regPath)
    {
        Assert.True(PathSafety.IsSafeRegistryKey(regPath));
    }
}
