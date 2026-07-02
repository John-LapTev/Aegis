using Aegis.Threats.Web;
using Xunit;

namespace Aegis.Threats.Tests.Web;

public class GoogleSearchTests
{
    private const string SampleJson = """
        {
          "items": [
            { "title": "Realtek Audio Driver 6.0.9659.1", "link": "https://www.realtek.com/downloads", "snippet": "Latest WHQL release." },
            { "title": "Second", "link": "https://example.com/2", "snippet": "desc 2" },
            { "title": "NoLink", "snippet": "skipped" }
          ]
        }
        """;

    [Fact]
    public void Parse_ReadsItems_SkipsNoLink()
    {
        var results = GoogleSearch.Parse(SampleJson, maxResults: 5);

        Assert.Equal(2, results.Count); // запись без link пропущена
        Assert.Equal("Realtek Audio Driver 6.0.9659.1", results[0].Title);
        Assert.Equal("https://www.realtek.com/downloads", results[0].Url);
        Assert.Equal("Latest WHQL release.", results[0].Snippet);
    }

    [Fact]
    public void Parse_RespectsMaxResults() =>
        Assert.Single(GoogleSearch.Parse(SampleJson, maxResults: 1));

    [Fact]
    public void Parse_NoItems_ReturnsEmpty() =>
        Assert.Empty(GoogleSearch.Parse("""{"searchInformation":{"totalResults":"0"}}""", 5));
}
