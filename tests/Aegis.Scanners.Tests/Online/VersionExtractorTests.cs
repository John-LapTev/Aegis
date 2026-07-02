using Aegis.Scanners.Online;
using Xunit;

namespace Aegis.Scanners.Tests.Online;

public class VersionExtractorTests
{
    [Theory]
    [InlineData("Nvidia GeForce Graphics Driver 610.62 for Windows 10/11", "610.62")]
    [InlineData("Realtek High Definition Audio Driver 6.0.9659.1", "6.0.9659.1")]
    [InlineData("Intel Graphics 31.0.101.5186 download", "31.0.101.5186")]
    public void Extract_PicksVersionToken(string text, string expected) =>
        Assert.Equal(expected, VersionExtractor.Extract([text]));

    [Fact]
    public void Extract_SkipsBareYear_TakesRealVersion() =>
        Assert.Equal("6.0.9", VersionExtractor.Extract(["Обновлено 2024.1", "версия 6.0.9"]));

    [Fact]
    public void Extract_NoVersion_ReturnsNull() =>
        Assert.Null(VersionExtractor.Extract(["no version here", "Windows 11"]));

    [Fact]
    public void Extract_UsesFirstTextWithVersion() =>
        Assert.Equal("2.5", VersionExtractor.Extract(["просто текст", "tool 2.5", "other 3.1"]));
}
