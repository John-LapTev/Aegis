using Aegis.Scanners.Internal;
using Xunit;

namespace Aegis.Scanners.Tests.Internal;

/// <summary>
/// Разбор вывода pnputil — чистая функция. Имена полей в выводе переведены на язык системы, поэтому
/// проверяем и русский, и английский вариант: опираться можно только на форму значений.
/// </summary>
public sealed class DriverPackageParserTests
{
    private const string RussianOutput = """
        Опубликованное имя:            oem12.inf
        Исходное имя:                  nvlt.inf
        Поставщик:                     NVIDIA
        Класс:                         Видеоадаптеры
        Дата и версия драйвера:        12.05.2024 31.0.15.3623

        Опубликованное имя:            oem7.inf
        Исходное имя:                  nvlt.inf
        Поставщик:                     NVIDIA
        Класс:                         Видеоадаптеры
        Дата и версия драйвера:        03.02.2023 30.0.14.9709

        Опубликованное имя:            oem3.inf
        Исходное имя:                  prnms003.inf
        Поставщик:                     Microsoft
        Класс:                         Принтеры
        Дата и версия драйвера:        21.06.2006 10.0.19041.1
        """;

    private const string EnglishOutput = """
        Published Name:     oem22.inf
        Original Name:      rtcamera.inf
        Provider Name:      Realtek
        Class Name:         Cameras
        Driver Version:     07/18/2023 10.0.22621.1

        Published Name:     oem23.inf
        Original Name:      rtcamera.inf
        Provider Name:      Realtek
        Class Name:         Cameras
        Driver Version:     11/02/2024 10.0.22621.9
        """;

    [Fact]
    public void ParsesRussianOutput()
    {
        var packages = DriverPackageParser.Parse(RussianOutput);

        Assert.Equal(3, packages.Count);
        var newest = packages.First(p => p.PublishedName == "oem12.inf");
        Assert.Equal("nvlt.inf", newest.OriginalName);
        Assert.Equal("31.0.15.3623", newest.Version);
        Assert.Equal(new DateOnly(2024, 5, 12), newest.Date);
    }

    [Fact]
    public void ParsesEnglishOutput_WithUsDateFormat()
    {
        var packages = DriverPackageParser.Parse(EnglishOutput);

        var package = packages.First(p => p.PublishedName == "oem22.inf");
        Assert.Equal("rtcamera.inf", package.OriginalName);
        Assert.Equal(new DateOnly(2023, 7, 18), package.Date);
    }

    [Fact]
    public void FindObsolete_KeepsNewestVersion()
    {
        var packages = DriverPackageParser.Parse(RussianOutput);

        var obsolete = DriverPackageParser.FindObsolete(packages, new HashSet<string>());

        // Устарел только oem7 (старая версия того же драйвера); oem12 — действующий, oem3 — единственный.
        Assert.Equal("oem7.inf", Assert.Single(obsolete).PublishedName);
    }

    [Fact]
    public void FindObsolete_NeverTouchesActivePackage()
    {
        var packages = DriverPackageParser.Parse(RussianOutput);

        // Даже если версия старее — пакет привязан к работающему устройству, значит удалять его нельзя.
        var obsolete = DriverPackageParser.FindObsolete(packages, new HashSet<string> { "oem7.inf" });

        Assert.Empty(obsolete);
    }

    [Fact]
    public void FindObsolete_SingleVersion_NothingToRemove()
    {
        var packages = DriverPackageParser.Parse("""
            Опубликованное имя:            oem5.inf
            Исходное имя:                  onlyone.inf
            Дата и версия драйвера:        01.01.2020 1.0.0.1
            """);

        Assert.Empty(DriverPackageParser.FindObsolete(packages, new HashSet<string>()));
    }

    [Fact]
    public void EmptyOutput_ParsesToNothing()
    {
        Assert.Empty(DriverPackageParser.Parse(string.Empty));
        Assert.Empty(DriverPackageParser.Parse("Microsoft PnP Utility\n\n"));
    }

    [Fact]
    public void ObsoleteChosenByVersion_NotByOrderInOutput()
    {
        // В выводе новая версия может идти первой или последней — решает номер версии, а не порядок строк.
        var packages = DriverPackageParser.Parse("""
            Опубликованное имя:            oem1.inf
            Исходное имя:                  same.inf
            Дата и версия драйвера:        01.01.2020 2.0.0.0

            Опубликованное имя:            oem2.inf
            Исходное имя:                  same.inf
            Дата и версия драйвера:        01.01.2024 1.0.0.0
            """);

        var obsolete = DriverPackageParser.FindObsolete(packages, new HashSet<string>());

        Assert.Equal("oem2.inf", Assert.Single(obsolete).PublishedName);
    }
}
