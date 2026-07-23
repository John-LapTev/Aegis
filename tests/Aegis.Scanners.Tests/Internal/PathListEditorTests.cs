using Aegis.Scanners.Internal;
using Xunit;

namespace Aegis.Scanners.Tests.Internal;

public sealed class PathListEditorTests
{
    private const string Sample = @"C:\Windows\system32;C:\Windows;C:\Gone\bin;C:\Tools\";

    [Fact]
    public void Remove_DropsOnlyRequestedEntry()
    {
        var result = PathListEditor.Remove(Sample, @"C:\Gone\bin");

        Assert.Equal(@"C:\Windows\system32;C:\Windows;C:\Tools\", result);
    }

    [Fact]
    public void Remove_IgnoresTrailingSlashAndCase()
    {
        Assert.Equal(@"C:\Windows\system32;C:\Windows;C:\Gone\bin",
            PathListEditor.Remove(Sample, @"c:\tools"));
    }

    [Fact]
    public void Remove_MissingEntry_ReturnsNull()
    {
        Assert.Null(PathListEditor.Remove(Sample, @"C:\Never\Existed"));
    }

    [Fact]
    public void Remove_WouldEmptyPath_ReturnsNull()
    {
        // Пустой Path ломает запуск программ по всей системе — такое значение писать нельзя никогда.
        Assert.Null(PathListEditor.Remove(@"C:\Only\One", @"C:\Only\One"));
    }

    [Fact]
    public void Remove_EmptyEntry_ReturnsNull()
    {
        Assert.Null(PathListEditor.Remove(Sample, "   "));
    }

    [Fact]
    public void FindMissing_ReportsOnlyAbsentDirectories()
    {
        var missing = PathListEditor.FindMissing(Sample, dir => !dir.Contains("Gone", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(@"C:\Gone\bin", Assert.Single(missing));
    }

    [Fact]
    public void FindMissing_SkipsVariablesAndNetworkPaths()
    {
        // %JAVA_HOME% разворачивает система, а сетевая папка может быть просто недоступна сейчас —
        // удалять такие записи нельзя.
        var value = @"%JAVA_HOME%\bin;\\server\share\tools;C:\Gone";

        var missing = PathListEditor.FindMissing(value, _ => false);

        Assert.Equal(@"C:\Gone", Assert.Single(missing));
    }

    [Fact]
    public void Split_IgnoresEmptySegments()
    {
        Assert.Equal([@"C:\A", @"C:\B"], PathListEditor.Split(@"C:\A;;  ;C:\B"));
    }
}
