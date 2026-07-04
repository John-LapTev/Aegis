using Aegis.Core.Models;
using Aegis.Scanners.Drivers;
using Xunit;

namespace Aegis.Scanners.Tests.Drivers;

public sealed class DriverUpdateMatcherTests
{
    private static readonly DriverUpdateOffer Audio = new()
    {
        Title = "Realtek - MEDIA",
        DeviceName = "Realtek High Definition Audio",
        HardwareId = @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0256",
    };

    private static readonly DriverUpdateOffer Net = new()
    {
        Title = "Intel - Net",
        DeviceName = "Intel Wi-Fi 6 AX201",
        HardwareId = @"PCI\VEN_8086&DEV_A0F0",
    };

    [Fact]
    public void Match_ByHardwareId_WhenInstanceIdContainsIt()
    {
        var offer = DriverUpdateMatcher.Match(
            [Audio, Net], @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0256&SUBSYS_11223344\4&xyz", "Звук");

        Assert.Same(Audio, offer);
    }

    [Fact]
    public void Match_ByName_WhenOfferNameIsSubstringOfInstalled()
    {
        // Имя предложения — вложенная часть установленного имени (консервативно, чтобы не спутать устройства).
        var gpu = new DriverUpdateOffer { Title = "NVIDIA - Display", DeviceName = "NVIDIA GeForce RTX 4060" };
        var offer = DriverUpdateMatcher.Match(
            [Audio, gpu], deviceId: null, deviceName: "NVIDIA GeForce RTX 4060 Laptop GPU");

        Assert.Same(gpu, offer);
    }

    [Fact]
    public void Match_ReturnsNull_WhenNothingMatches()
    {
        var offer = DriverUpdateMatcher.Match([Audio, Net], @"USB\VID_1234&PID_5678\abc", "Мышь неизвестная");

        Assert.Null(offer);
    }

    [Fact]
    public void Match_HardwareIdWins_OverNameFallback()
    {
        // deviceId точно указывает на сеть; имя пустое → берём по железу, не угадываем по имени.
        var offer = DriverUpdateMatcher.Match([Audio, Net], @"PCI\VEN_8086&DEV_A0F0\3&11", deviceName: null);

        Assert.Same(Net, offer);
    }

    [Fact]
    public void Match_EmptyOffers_ReturnsNull() =>
        Assert.Null(DriverUpdateMatcher.Match([], @"HDAUDIO\X", "Realtek High Definition Audio"));
}
