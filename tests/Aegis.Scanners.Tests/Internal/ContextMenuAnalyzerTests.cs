using Aegis.Scanners.Internal;
using Xunit;

namespace Aegis.Scanners.Tests.Internal;

public sealed class ContextMenuAnalyzerTests
{
    [Fact]
    public void ExtractsQuotedPath()
    {
        Assert.Equal(@"C:\Program Files\App\app.exe",
            ContextMenuAnalyzer.ExtractExecutablePath("\"C:\\Program Files\\App\\app.exe\" \"%1\""));
    }

    [Fact]
    public void ExtractsUnquotedPathWithSpaces()
    {
        Assert.Equal(@"C:\Program Files\App\app.exe",
            ContextMenuAnalyzer.ExtractExecutablePath(@"C:\Program Files\App\app.exe %1"));
    }

    [Fact]
    public void ExtractsDllPath()
    {
        Assert.Equal(@"C:\Windows\System32\shell32.dll",
            ContextMenuAnalyzer.ExtractExecutablePath(@"rundll32.exe C:\Windows\System32\shell32.dll,Control_RunDLL"));
    }

    [Fact]
    public void NoPath_ReturnsNull()
    {
        Assert.Null(ContextMenuAnalyzer.ExtractExecutablePath("ms-settings:display"));
        Assert.Null(ContextMenuAnalyzer.ExtractExecutablePath(""));
        Assert.Null(ContextMenuAnalyzer.ExtractExecutablePath(null));
    }

    [Fact]
    public void IsBroken_MissingFile_True()
    {
        Assert.True(ContextMenuAnalyzer.IsBroken(@"""C:\Gone\old.exe"" ""%1""", _ => false));
    }

    [Fact]
    public void IsBroken_ExistingFile_False()
    {
        Assert.False(ContextMenuAnalyzer.IsBroken(@"""C:\App\app.exe"" ""%1""", _ => true));
    }

    [Fact]
    public void IsBroken_UnparseableCommand_False()
    {
        // Не поняли команду — не трогаем: лишний пункт в меню безопаснее сломанного меню.
        Assert.False(ContextMenuAnalyzer.IsBroken("ms-settings:display", _ => false));
    }

    [Fact]
    public void IsBroken_PathWithEnvironmentVariable_False()
    {
        // %SystemRoot% и подобное раскрывает система; без раскрытия проверять существование бессмысленно.
        Assert.False(ContextMenuAnalyzer.IsBroken(@"%SystemRoot%\system32\notepad.exe %1", _ => false));
    }

    [Fact]
    public void DisplayName_PrefersLabel()
    {
        Assert.Equal("Открыть в редакторе", ContextMenuAnalyzer.DisplayName("EditWithApp", "Открыть в редакторе"));
        Assert.Equal("EditWithApp", ContextMenuAnalyzer.DisplayName("EditWithApp", "   "));
    }
}
