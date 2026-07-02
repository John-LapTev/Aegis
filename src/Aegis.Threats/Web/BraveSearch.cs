using System.Net.Http;
using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Web;

/// <summary>
/// Веб-поиск через официальный Brave Search API (нужен ключ, бесплатный тариф ~2000/мес, без антибот-«anomaly»).
/// Используется как ОСНОВНОЙ провайдер, когда ключ задан; иначе остаётся бесплатный DuckDuckGo. Best-effort:
/// при ошибке/лимите/таймауте возвращаем пустой список (вызывающий откатится на запасной поиск).
/// </summary>
public sealed class BraveSearch : IWebSearch
{
    private const string Endpoint = "https://api.search.brave.com/res/v1/web/search";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public BraveSearch(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

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

            var url = $"{Endpoint}?q={Uri.EscapeDataString(query)}&count={Math.Clamp(maxResults, 1, 20)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("X-Subscription-Token", _apiKey);

            using var response = await _http.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            return Parse(json, maxResults);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return [];
        }
    }

    /// <summary>Разбирает JSON Brave (web.results[].{title,url,description}) в наши результаты.</summary>
    public static IReadOnlyList<WebSearchResult> Parse(string json, int maxResults)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("web", out var web) ||
            !web.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<WebSearchResult>();
        foreach (var item in results.EnumerateArray())
        {
            if (list.Count >= maxResults)
            {
                break;
            }

            var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(title))
            {
                continue;
            }

            var snippet = item.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            list.Add(new WebSearchResult { Title = title, Url = url, Snippet = StripTags(snippet) });
        }

        return list;
    }

    // Brave иногда подсвечивает совпадения тегами <strong> — убираем для чистого текста.
    private static string StripTags(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);
}
