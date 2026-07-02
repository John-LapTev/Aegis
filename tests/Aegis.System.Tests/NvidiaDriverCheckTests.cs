using Aegis.System.Internal;
using Aegis.System.Probes;
using Xunit;

namespace Aegis.System.Tests;

public sealed class NvidiaDriverCheckTests
{
    [Theory]
    [InlineData("32.0.15.7652", "576.52")]
    [InlineData("31.0.15.5763", "557.63")]
    public void NvidiaVersion_FromWin32_TakesLastFiveDigits(string win32, string expected) =>
        Assert.Equal(expected, NvidiaDriverCheck.NvidiaVersion(win32));

    [Fact]
    public void ParseLatest_ValidResponse_ReturnsVersionAndUrl()
    {
        const string json =
            "{\"Success\":\"1\",\"IDS\":[{\"downloadInfo\":{\"Version\":\"610.62\",\"DownloadURL\":\"https://x/610.62.exe\"}}]}";

        var (version, url) = NvidiaDriverCheck.ParseLatest(json);

        Assert.Equal("610.62", version);
        Assert.Equal("https://x/610.62.exe", url);
    }

    [Fact]
    public void ParseLatest_Empty_ReturnsNulls()
    {
        var (version, url) = NvidiaDriverCheck.ParseLatest("{\"IDS\":[]}");
        Assert.Null(version);
        Assert.Null(url);
    }

    [Theory]
    [InlineData("32.0.15.7652", "610.62", true)]    // установлено 576.52 < 610.62 → новее
    [InlineData("32.0.16.1062", "610.62", false)]   // установлено 610.62 = 610.62 → не новее
    public void IsNewer_ComparesNvidiaVersions(string installedWin32, string latest, bool expected) =>
        Assert.Equal(expected, NvidiaDriverCheck.IsNewer(installedWin32, latest));

    [Fact]
    public void NvidiaGpuData_EmbeddedResource_MapsKnownGpu()
    {
        Assert.True(NvidiaGpuData.Instance.Count > 100);
        Assert.Equal("933", NvidiaGpuData.Instance.FindPfid("NVIDIA GeForce RTX 3070"));
    }
}
