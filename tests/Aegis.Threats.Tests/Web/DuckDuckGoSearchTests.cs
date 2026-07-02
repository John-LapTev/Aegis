using Aegis.Threats.Web;
using Xunit;

namespace Aegis.Threats.Tests.Web;

public class DuckDuckGoSearchTests
{
    private const string SampleHtml = """
        <div class="result results_links">
          <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.nvidia.com%2Fdrivers&amp;rut=abc">Download NVIDIA Drivers</a>
          <a class="result__snippet" href="//x">Latest <b>GeForce</b> driver 610.62 WHQL.</a>
        </div>
        <div class="result results_links">
          <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpage&amp;rut=def">Second Result &amp; More</a>
          <a class="result__snippet" href="//y">Some description here.</a>
        </div>
        """;

    [Fact]
    public void Parse_ExtractsTitleRealUrlAndSnippet()
    {
        var results = DuckDuckGoSearch.Parse(SampleHtml, maxResults: 5);

        Assert.Equal(2, results.Count);
        Assert.Equal("Download NVIDIA Drivers", results[0].Title);
        Assert.Equal("https://www.nvidia.com/drivers", results[0].Url);            // uddg раскодирован
        Assert.Equal("Latest GeForce driver 610.62 WHQL.", results[0].Snippet);    // теги убраны
        Assert.Equal("Second Result & More", results[1].Title);                    // HTML-сущности раскодированы
        Assert.Equal("https://example.com/page", results[1].Url);
    }

    [Fact]
    public void Parse_RespectsMaxResults()
    {
        var results = DuckDuckGoSearch.Parse(SampleHtml, maxResults: 1);
        Assert.Single(results);
    }

    [Fact]
    public void Parse_EmptyOrJunkHtml_ReturnsEmpty()
    {
        Assert.Empty(DuckDuckGoSearch.Parse("<html><body>no results</body></html>", 5));
        Assert.Empty(DuckDuckGoSearch.Parse(string.Empty, 5));
    }
}
