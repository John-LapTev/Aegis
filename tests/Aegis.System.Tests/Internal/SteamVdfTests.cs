using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

public sealed class SteamVdfTests
{
    [Fact]
    public void ParseLibraryPaths_ReturnsAllPaths_Unescaped()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                }
                "1"
                {
                    "path"		"D:\\SteamLibrary"
                }
            }
            """;

        var paths = SteamVdf.ParseLibraryPaths(vdf);

        Assert.Equal(2, paths.Count);
        Assert.Contains(@"C:\Program Files (x86)\Steam", paths);
        Assert.Contains(@"D:\SteamLibrary", paths);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("\"libraryfolders\" { }")]
    public void ParseLibraryPaths_EmptyOrNoPaths_ReturnsEmpty(string? vdf) =>
        Assert.Empty(SteamVdf.ParseLibraryPaths(vdf));

    [Theory]
    [InlineData("appmanifest_730.acf", "730")]
    [InlineData("appmanifest_1091500.acf", "1091500")]
    public void AppIdFromManifest_ValidName_ReturnsAppId(string fileName, string expected) =>
        Assert.Equal(expected, SteamVdf.AppIdFromManifest(fileName));

    [Theory]
    [InlineData("appmanifest_.acf")]
    [InlineData("something.txt")]
    [InlineData("")]
    [InlineData(null)]
    public void AppIdFromManifest_Invalid_ReturnsNull(string? fileName) =>
        Assert.Null(SteamVdf.AppIdFromManifest(fileName));
}
