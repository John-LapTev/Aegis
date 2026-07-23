using Aegis.Scanners.Internal;
using Xunit;

namespace Aegis.Scanners.Tests.Internal;

/// <summary>
/// Разбор списка обновлений winget. Заголовки таблицы переведены на язык системы и ширина колонок плавает,
/// поэтому проверяем оба языка и «хвост» вывода, который таблицей не является.
/// </summary>
public sealed class WingetUpgradeParserTests
{
    private const string RussianOutput = """
        Название                 Идентификатор            Версия      Доступно    Источник
        ----------------------------------------------------------------------------------
        7-Zip                    7zip.7zip                23.01       24.09       winget
        Mozilla Firefox          Mozilla.Firefox          128.0       131.0.2     winget
        Доступно обновлений: 2
        """;

    private const string EnglishOutput = """
        Name                Id                     Version     Available   Source
        -----------------------------------------------------------------------------
        VLC media player    VideoLAN.VLC           3.0.20      3.0.21      winget
        Notepad++           Notepad++.Notepad++    8.6.9       8.7.1       winget
        2 upgrades available.
        """;

    [Fact]
    public void ParsesRussianTable()
    {
        var upgrades = WingetUpgradeParser.Parse(RussianOutput);

        Assert.Equal(2, upgrades.Count);
        Assert.Equal("7-Zip", upgrades[0].Name);
        Assert.Equal("7zip.7zip", upgrades[0].Id);
        Assert.Equal("23.01", upgrades[0].CurrentVersion);
        Assert.Equal("24.09", upgrades[0].AvailableVersion);
    }

    [Fact]
    public void ParsesEnglishTable_WithSpacesInNames()
    {
        var upgrades = WingetUpgradeParser.Parse(EnglishOutput);

        Assert.Equal("VLC media player", upgrades[0].Name);
        Assert.Equal("3.0.21", upgrades[0].AvailableVersion);
    }

    [Fact]
    public void SkipsSummaryLine()
    {
        Assert.DoesNotContain(WingetUpgradeParser.Parse(RussianOutput), u => u.Name.Contains("обновлений"));
        Assert.DoesNotContain(WingetUpgradeParser.Parse(EnglishOutput), u => u.Name.Contains("upgrades"));
    }

    [Fact]
    public void SkipsEntriesWithUnknownVersion()
    {
        // «Unknown» означает, что winget не смог определить версию — обновлять такое вслепую нельзя.
        const string output = """
            Name           Id              Version     Available   Source
            ---------------------------------------------------------------
            Weird App      Some.App        Unknown     Unknown     winget
            Good App       Good.App        1.0         2.0         winget
            """;

        var upgrades = WingetUpgradeParser.Parse(output);

        Assert.Equal("Good App", Assert.Single(upgrades).Name);
    }

    [Fact]
    public void EmptyOrNonTableOutput_ReturnsNothing()
    {
        Assert.Empty(WingetUpgradeParser.Parse(string.Empty));
        Assert.Empty(WingetUpgradeParser.Parse("Не удалось найти установленный пакет"));
        Assert.Empty(WingetUpgradeParser.Parse("Все программы обновлены."));
    }
}
