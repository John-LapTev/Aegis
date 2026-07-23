using Aegis.Scanners.Internal;
using Xunit;

namespace Aegis.Scanners.Tests.Internal;

public sealed class GameProcessCatalogTests
{
    [Fact]
    public void FindRunningGame_DetectsKnownGame_RegardlessOfExtension()
    {
        // Из «Диспетчера задач» имена приходят без «.exe», из tasklist — с ним: работать должно в обоих видах.
        Assert.Equal("cs2.exe", GameProcessCatalog.FindRunningGame(["explorer", "cs2", "chrome"]));
        Assert.Equal("cs2.exe", GameProcessCatalog.FindRunningGame(["explorer.exe", "cs2.exe"]));
    }

    [Fact]
    public void FindRunningGame_NoGame_ReturnsNull()
    {
        Assert.Null(GameProcessCatalog.FindRunningGame(["explorer.exe", "chrome.exe", "notepad.exe"]));
    }

    [Fact]
    public void FindRunningGame_UsesCustomList()
    {
        Assert.Equal("mygame.exe", GameProcessCatalog.FindRunningGame(["mygame.exe"], ["mygame.exe"]));
        Assert.Equal("mygame.exe", GameProcessCatalog.FindRunningGame(["mygame.exe"], ["MyGame"]));
    }

    [Fact]
    public void FindRunningGame_DoesNotMatchBySubstring()
    {
        // «notcs2launcher.exe» — не cs2: совпадение только по полному имени файла, иначе режим включался бы
        // от постороннего процесса с похожим названием.
        Assert.Null(GameProcessCatalog.FindRunningGame(["notcs2launcher.exe", "cs2helper.exe"]));
    }

    [Fact]
    public void IsClosable_ProtectsSystemProcesses()
    {
        Assert.False(GameProcessCatalog.IsClosable("explorer.exe"));
        Assert.False(GameProcessCatalog.IsClosable("csrss.exe"));
        Assert.False(GameProcessCatalog.IsClosable("lsass.exe"));
        Assert.False(GameProcessCatalog.IsClosable("svchost.exe"));
        Assert.False(GameProcessCatalog.IsClosable("aegis.exe")); // сама программа
    }

    [Fact]
    public void IsClosable_AllowsKnownBackgroundApps()
    {
        Assert.True(GameProcessCatalog.IsClosable("chrome.exe"));
        Assert.True(GameProcessCatalog.IsClosable("Discord.exe"));
        Assert.True(GameProcessCatalog.IsClosable("GoogleUpdate.exe"));
    }

    [Fact]
    public void IsClosable_UnknownProcess_NotTouched()
    {
        // Незнакомую программу не закрываем: вдруг это рабочий инструмент с несохранёнными данными.
        Assert.False(GameProcessCatalog.IsClosable("some-users-app.exe"));
    }

    [Fact]
    public void NoGameIsAlsoListedAsClosable()
    {
        // Защита от опечатки в данных: игра никогда не должна попасть в список закрываемых программ.
        foreach (var app in GameProcessCatalog.BackgroundApps)
        {
            Assert.DoesNotContain(app.ProcessName, GameProcessCatalog.KnownGames);
        }
    }

    [Fact]
    public void ProtectedProcessesAreNeverInBackgroundList()
    {
        foreach (var app in GameProcessCatalog.BackgroundApps)
        {
            Assert.DoesNotContain(app.ProcessName, GameProcessCatalog.Protected);
        }
    }

    [Fact]
    public void DisplayName_KnownApp_IsHumanReadable()
    {
        Assert.Equal("Google Chrome", GameProcessCatalog.DisplayName("chrome.exe"));
        Assert.Equal("unknown.exe", GameProcessCatalog.DisplayName("unknown.exe"));
    }
}
