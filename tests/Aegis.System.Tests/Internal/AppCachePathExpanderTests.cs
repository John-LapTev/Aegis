using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

public sealed class AppCachePathExpanderTests
{
    [Fact]
    public void ExpandVariables_KnownToken_Substituted()
    {
        var result = AppCachePathExpander.ExpandVariables(@"%LocalAppData%\Google\Chrome");

        // Чистая подстановка: токен заменён на путь, остальное — дословно (без нормализации сепараторов).
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome";
        Assert.Equal(expected, result);
        Assert.DoesNotContain('%', result);
    }

    [Fact]
    public void ExpandVariables_UnknownToken_LeftAsIs()
    {
        const string pattern = @"%TotallyUnknownVar%\Foo";
        Assert.Equal(pattern, AppCachePathExpander.ExpandVariables(pattern));
    }

    [Fact]
    public void ResolveExistingDirectories_PatternWithUnknownVar_ReturnsEmpty() =>
        Assert.Empty(AppCachePathExpander.ResolveExistingDirectories(@"%TotallyUnknownVar%\Foo\Bar"));
}
