using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

/// <summary>
/// Каталог «лишних» UWP должен ловить заведомо хлам по префиксу имени пакета и НЕ трогать нужное
/// (Калькулятор, Магазин, Терминал, Почта и т.п.). Чистая логика — на любой ОС.
/// </summary>
public sealed class AppxBloatCatalogTests
{
    [Fact]
    public void Match_PicksKnownBloat_ByPackagePrefix()
    {
        string[] installed =
        [
            "king.com.CandyCrushSaga_1.2.3.0_x64__kgqvnymyfvs32",
            "Microsoft.BingNews_4.55.0_x64__8wekyb3d8bbwe",
            "Microsoft.WindowsCalculator_11.2_x64__8wekyb3d8bbwe", // нужное — не трогаем
            "Microsoft.WindowsStore_22.0_x64__8wekyb3d8bbwe",      // нужное — не трогаем
        ];

        var bloat = AppxBloatCatalog.Match(installed);

        Assert.Equal(2, bloat.Count);
        Assert.Contains(bloat, a => a.PackageFullName.StartsWith("king.com.", StringComparison.Ordinal));
        Assert.Contains(bloat, a => a.Name.Contains("Bing News"));
        Assert.DoesNotContain(bloat, a => a.PackageFullName.Contains("Calculator"));
        Assert.DoesNotContain(bloat, a => a.PackageFullName.Contains("WindowsStore"));
    }

    [Fact]
    public void Match_EmptyAndBlankLines_Ignored()
    {
        string[] input = ["", "   ", "Microsoft.WindowsTerminal_1.0_x64__8wekyb3d8bbwe"];

        Assert.Empty(AppxBloatCatalog.Match(input));
    }

    [Fact]
    public void Match_AssignsFriendlyNameAndCategory()
    {
        var bloat = AppxBloatCatalog.Match(["Microsoft.MicrosoftSolitaireCollection_4.0_x64__8wekyb3d8bbwe"]);

        var app = Assert.Single(bloat);
        Assert.False(string.IsNullOrWhiteSpace(app.Name));
        Assert.False(string.IsNullOrWhiteSpace(app.Category));
    }
}
