using System;
using Aegis.Core;
using Xunit;

namespace Aegis.Core.Tests;

public sealed class UpdateVersionTests
{
    [Theory]
    [InlineData("v2.78.0", 2, 78, 0)]
    [InlineData("V2.78", 2, 78, 0)]
    [InlineData("2.78.0.5", 2, 78, 0)]
    [InlineData("  v2.9.1  ", 2, 9, 1)]
    public void Parse_ValidTags(string tag, int major, int minor, int build)
    {
        var v = UpdateVersion.Parse(tag);

        Assert.NotNull(v);
        Assert.Equal(new Version(major, minor, build), v);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("не версия")]
    [InlineData("v")]
    public void Parse_InvalidTags_ReturnNull(string? tag)
    {
        Assert.Null(UpdateVersion.Parse(tag));
    }

    [Fact]
    public void IsNewer_NewerTag_True()
    {
        Assert.True(UpdateVersion.IsNewer("v2.78.0", new Version(2, 77, 0)));
        Assert.True(UpdateVersion.IsNewer("2.77.1", new Version(2, 77, 0)));
        Assert.True(UpdateVersion.IsNewer("v3.0.0", new Version(2, 99, 9)));
    }

    [Fact]
    public void IsNewer_SameOrOlder_False()
    {
        Assert.False(UpdateVersion.IsNewer("v2.77.0", new Version(2, 77, 0)));      // равны
        Assert.False(UpdateVersion.IsNewer("v2.76.0", new Version(2, 77, 0)));      // старее
        Assert.False(UpdateVersion.IsNewer("v2.77.0", new Version(2, 77, 0, 5)));   // редакция игнорируется → равны
    }

    [Fact]
    public void IsNewer_UnparseableOrNull_False()
    {
        Assert.False(UpdateVersion.IsNewer("мусор", new Version(2, 77, 0)));
        Assert.False(UpdateVersion.IsNewer(null, new Version(2, 77, 0)));
        Assert.False(UpdateVersion.IsNewer("v2.78.0", null));
    }
}
