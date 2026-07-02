using Microsoft.Win32;
using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

/// <summary>
/// Регресс на тихую порчу реестра при откате: бэкап значения должен сохранять ТИП, а не приводить всё
/// к строке. Раньше Binary/MultiString терялись уже при бэкапе, а QWord/Binary портились при восстановлении.
/// Кодек чистый — round-trip проверяется на любой ОС.
/// </summary>
public sealed class RegistryValueCodecTests
{
    [Fact]
    public void DWord_RoundTrips()
    {
        var encoded = RegistryValueCodec.Encode(1, RegistryValueKind.DWord);
        Assert.Equal(1, Assert.IsType<int>(RegistryValueCodec.Decode(encoded, RegistryValueKind.DWord)));
    }

    [Fact]
    public void QWord_RoundTrips_AsLong()
    {
        const long original = 9_000_000_000L; // больше int — раньше ломалось
        var encoded = RegistryValueCodec.Encode(original, RegistryValueKind.QWord);
        Assert.Equal(original, Assert.IsType<long>(RegistryValueCodec.Decode(encoded, RegistryValueKind.QWord)));
    }

    [Fact]
    public void Binary_RoundTrips_AsBytes()
    {
        byte[] original = [0x00, 0x10, 0xFF, 0x42];
        var encoded = RegistryValueCodec.Encode(original, RegistryValueKind.Binary);
        Assert.Equal(original, Assert.IsType<byte[]>(RegistryValueCodec.Decode(encoded, RegistryValueKind.Binary)));
    }

    [Fact]
    public void MultiString_RoundTrips_AsArray()
    {
        string[] original = ["один", "два", "три"];
        var encoded = RegistryValueCodec.Encode(original, RegistryValueKind.MultiString);
        Assert.Equal(original, Assert.IsType<string[]>(RegistryValueCodec.Decode(encoded, RegistryValueKind.MultiString)));
    }

    [Theory]
    [InlineData(RegistryValueKind.String)]
    [InlineData(RegistryValueKind.ExpandString)]
    public void StringKinds_RoundTrip(RegistryValueKind kind)
    {
        const string original = @"C:\Program Files\App\app.exe";
        var encoded = RegistryValueCodec.Encode(original, kind);
        Assert.Equal(original, Assert.IsType<string>(RegistryValueCodec.Decode(encoded, kind)));
    }

    [Fact]
    public void Decode_GarbageBinary_DoesNotThrow() =>
        Assert.Empty(Assert.IsType<byte[]>(RegistryValueCodec.Decode("не-base64!!!", RegistryValueKind.Binary)));
}
