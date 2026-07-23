using Aegis.Core.Models;
using Aegis.System.Optimize;
using Xunit;

namespace Aegis.System.Tests.Optimize;

/// <summary>
/// Проверка снимка игрового режима, прочитанного с диска. Файл лежит в профиле пользователя и может быть
/// подменён — тогда «выключение режима» стало бы способом записать в реестр что угодно нашими правами
/// администратора. Поэтому всё лишнее из снимка выбрасывается (приём подсмотрен в Kudu).
/// </summary>
public sealed class GameModeSnapshotStoreTests
{
    [Fact]
    public void Sanitize_KeepsKnownServices_DropsForeignOnes()
    {
        var snapshot = Snapshot() with
        {
            Services =
            [
                new GameModeServiceState { Name = "WSearch", StartType = 2, WasRunning = true },
                new GameModeServiceState { Name = "WinDefend", StartType = 2, WasRunning = true },   // не наша
                new GameModeServiceState { Name = "МойВирус", StartType = 2, WasRunning = true },    // подмена
            ],
        };

        var clean = GameModeSnapshotStore.Sanitize(snapshot);

        Assert.Equal("WSearch", Assert.Single(clean.Services).Name);
    }

    [Fact]
    public void Sanitize_RejectsInvalidStartType()
    {
        var snapshot = Snapshot() with
        {
            Services = [new GameModeServiceState { Name = "WSearch", StartType = 99, WasRunning = true }],
        };

        Assert.Empty(GameModeSnapshotStore.Sanitize(snapshot).Services);
    }

    [Fact]
    public void Sanitize_KeepsAllowedRegistryValues()
    {
        var snapshot = Snapshot() with
        {
            RegistryValues =
            [
                new GameModeRegistryState
                {
                    Hive = "HKCU", SubKey = @"System\GameConfigStore", ValueName = "GameDVR_Enabled", Value = 1,
                },
            ],
        };

        Assert.Single(GameModeSnapshotStore.Sanitize(snapshot).RegistryValues);
    }

    [Fact]
    public void Sanitize_DropsArbitraryRegistryPaths()
    {
        // Классическая попытка через подменённый снимок: «восстановить» автозапуск чужой программы.
        var snapshot = Snapshot() with
        {
            RegistryValues =
            [
                new GameModeRegistryState
                {
                    Hive = "HKLM",
                    SubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    ValueName = "Miner",
                    Value = 1,
                },
                new GameModeRegistryState
                {
                    Hive = "HKLM",
                    SubKey = @"SYSTEM\CurrentControlSet\Services\WinDefend",
                    ValueName = "Start",
                    Value = 4,
                },
            ],
        };

        Assert.Empty(GameModeSnapshotStore.Sanitize(snapshot).RegistryValues);
    }

    [Fact]
    public void Sanitize_NetworkInterface_OnlyWithGuidPathAndKnownValue()
    {
        var good = new GameModeRegistryState
        {
            Hive = "HKLM",
            SubKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{2f1e3b7c-9a1d-4f2e-8b0c-5a6d7e8f9012}",
            ValueName = "TcpAckFrequency",
            Value = 2,
        };
        var badPath = good with { SubKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\..\..\Evil" };
        var badValue = good with { ValueName = "Start" };

        Assert.True(GameModeSnapshotStore.IsAllowedRegistryValue(good));
        Assert.False(GameModeSnapshotStore.IsAllowedRegistryValue(badPath));
        Assert.False(GameModeSnapshotStore.IsAllowedRegistryValue(badValue));
    }

    [Fact]
    public void Sanitize_RejectsNonGuidPowerScheme()
    {
        var snapshot = Snapshot() with { PowerSchemeGuid = "; shutdown /r" };

        Assert.Null(GameModeSnapshotStore.Sanitize(snapshot).PowerSchemeGuid);
    }

    [Fact]
    public void Sanitize_KeepsValidPowerScheme()
    {
        var snapshot = Snapshot() with { PowerSchemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e" };

        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", GameModeSnapshotStore.Sanitize(snapshot).PowerSchemeGuid);
    }

    private static GameModeSnapshot Snapshot() => new() { ActivatedAt = DateTimeOffset.UnixEpoch };
}
