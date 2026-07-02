using Aegis.Threats.Web;
using Xunit;

namespace Aegis.Threats.Tests.Web;

public class TavilySerperSearchTests
{
    private const string TavilyJson = """
        {
          "query": "x",
          "results": [
            { "title": "NVIDIA 610.62", "url": "https://www.nvidia.com/drivers", "content": "Latest GeForce driver." },
            { "title": "Second", "url": "https://example.com/2", "content": "desc" },
            { "title": "NoUrl", "content": "skipped" }
          ]
        }
        """;

    private const string SerperJson = """
        {
          "organic": [
            { "title": "GeForce Drivers", "link": "https://www.nvidia.com/drivers", "snippet": "Official 610.62." },
            { "title": "Second", "link": "https://example.com/2", "snippet": "desc" },
            { "title": "NoLink", "snippet": "skipped" }
          ]
        }
        """;

    [Fact]
    public void Tavily_Parse_ReadsResults_SkipsNoUrl()
    {
        var r = TavilySearch.Parse(TavilyJson, 5);
        Assert.Equal(2, r.Count);
        Assert.Equal("NVIDIA 610.62", r[0].Title);
        Assert.Equal("https://www.nvidia.com/drivers", r[0].Url);
        Assert.Equal("Latest GeForce driver.", r[0].Snippet);
    }

    [Fact]
    public void Serper_Parse_ReadsOrganic_SkipsNoLink()
    {
        var r = SerperSearch.Parse(SerperJson, 5);
        Assert.Equal(2, r.Count);
        Assert.Equal("GeForce Drivers", r[0].Title);
        Assert.Equal("https://www.nvidia.com/drivers", r[0].Url);
        Assert.Equal("Official 610.62.", r[0].Snippet);
    }

    [Fact]
    public void Parse_RespectsMaxResults()
    {
        Assert.Single(TavilySearch.Parse(TavilyJson, 1));
        Assert.Single(SerperSearch.Parse(SerperJson, 1));
    }

    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        Assert.Empty(TavilySearch.Parse("""{"results":[]}""", 5));
        Assert.Empty(SerperSearch.Parse("""{"x":1}""", 5));
    }
}
