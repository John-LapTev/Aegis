using Aegis.Threats.Web;
using Xunit;

namespace Aegis.Threats.Tests.Web;

public class BraveSearchTests
{
    private const string SampleJson = """
        {
          "web": {
            "results": [
              { "title": "Realtek Audio Driver 6.0.9659.1", "url": "https://www.realtek.com/downloads", "description": "Latest <strong>WHQL</strong> release." },
              { "title": "Second", "url": "https://example.com/2", "description": "desc 2" },
              { "title": "NoUrl", "description": "skipped" }
            ]
          }
        }
        """;

    [Fact]
    public void Parse_ReadsResults_StripsTags_SkipsNoUrl()
    {
        var results = BraveSearch.Parse(SampleJson, maxResults: 5);

        Assert.Equal(2, results.Count); // запись без url пропущена
        Assert.Equal("Realtek Audio Driver 6.0.9659.1", results[0].Title);
        Assert.Equal("https://www.realtek.com/downloads", results[0].Url);
        Assert.Equal("Latest WHQL release.", results[0].Snippet); // теги убраны
    }

    [Fact]
    public void Parse_RespectsMaxResults() =>
        Assert.Single(BraveSearch.Parse(SampleJson, maxResults: 1));

    [Fact]
    public void Parse_MissingWebOrResults_ReturnsEmpty()
    {
        Assert.Empty(BraveSearch.Parse("""{"x":1}""", 5));
        Assert.Empty(BraveSearch.Parse("""{"web":{}}""", 5));
    }
}
