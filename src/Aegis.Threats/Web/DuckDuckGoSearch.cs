using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Web;

/// <summary>
/// Веб-поиск через DuckDuckGo (HTML-версия) — БЕЗ ключа и регистрации, работает на любой машине.
/// Парсим выдачу html.duckduckgo.com: заголовок, реальная ссылка (из параметра uddg) и описание.
/// Best-effort: при любой ошибке/таймауте возвращаем пустой список (ИИ ответит по своим знаниям).
/// </summary>
public sealed partial class DuckDuckGoSearch : IWebSearch
{
    private readonly HttpClient _http;

    public DuckDuckGoSearch(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(
        string query, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(12));

            using var request = new HttpRequestMessage(
                HttpMethod.Get, "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query));
            // Без человеческого User-Agent DuckDuckGo отдаёт пустую/капчевую страницу.
            request.Headers.TryAddWithoutValidation(
                "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");

            using var response = await _http.SendAsync(request, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            return Parse(html, maxResults);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Нет сети / таймаут / отмена — поиск просто не выполнен (не ошибка для пользователя).
            return [];
        }
    }

    /// <summary>Разбирает HTML-выдачу DuckDuckGo в список результатов (заголовок + реальная ссылка + описание).</summary>
    public static IReadOnlyList<WebSearchResult> Parse(string html, int maxResults)
    {
        var titles = LinkRegex().Matches(html);
        var snippets = SnippetRegex().Matches(html);
        var results = new List<WebSearchResult>();

        for (var i = 0; i < titles.Count && results.Count < maxResults; i++)
        {
            var href = titles[i].Groups[1].Value;
            var url = ExtractRealUrl(href);
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            var title = Clean(titles[i].Groups[2].Value);
            var snippet = i < snippets.Count ? Clean(snippets[i].Groups[1].Value) : string.Empty;
            if (title.Length > 0)
            {
                results.Add(new WebSearchResult { Title = title, Url = url, Snippet = snippet });
            }
        }

        return results;
    }

    /// <summary>Из href вида //duckduckgo.com/l/?uddg=ENCODED&amp;rut=… достаёт настоящий URL.</summary>
    private static string ExtractRealUrl(string href)
    {
        var match = UddgRegex().Match(href);
        if (match.Success)
        {
            return Uri.UnescapeDataString(match.Groups[1].Value);
        }

        // На случай прямой ссылки (без редиректа uddg).
        return href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : string.Empty;
    }

    private static string Clean(string raw) =>
        WebUtility.HtmlDecode(TagRegex().Replace(raw, string.Empty)).Trim();

    [GeneratedRegex("""class="result__a"[^>]*href="([^"]+)"[^>]*>(.*?)</a>""", RegexOptions.Singleline)]
    private static partial Regex LinkRegex();

    [GeneratedRegex("""class="result__snippet"[^>]*>(.*?)</a>""", RegexOptions.Singleline)]
    private static partial Regex SnippetRegex();

    [GeneratedRegex(@"uddg=([^&""]+)")]
    private static partial Regex UddgRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
