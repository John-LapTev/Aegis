using System.Net.Http;
using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Web;

/// <summary>
/// Веб-поиск через Google Custom Search JSON API (нужны ключ + ID поисковика «cx»; бесплатно ~100/день, БЕЗ карты).
/// Используется как основной провайдер, когда заданы ключ и cx; иначе DuckDuckGo. Best-effort: при ошибке/лимите
/// возвращаем пустой список (вызывающий откатится на запасной поиск).
/// </summary>
public sealed class GoogleSearch : IWebSearch
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _searchEngineId;

    public GoogleSearch(HttpClient http, string apiKey, string searchEngineId)
    {
        _http = http;
        _apiKey = apiKey;
        _searchEngineId = searchEngineId;
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

            var url = $"{Endpoint}?key={Uri.EscapeDataString(_apiKey)}&cx={Uri.EscapeDataString(_searchEngineId)}" +
                      $"&q={Uri.EscapeDataString(query)}&num={Math.Clamp(maxResults, 1, 10)}";

            using var response = await _http.GetAsync(url, cts.Token).ConfigureAwait(false);
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

    /// <summary>Разбирает JSON Google (items[].{title,link,snippet}) в наши результаты.</summary>
    public static IReadOnlyList<WebSearchResult> Parse(string json, int maxResults)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<WebSearchResult>();
        foreach (var item in items.EnumerateArray())
        {
            if (list.Count >= maxResults)
            {
                break;
            }

            var url = item.TryGetProperty("link", out var l) ? l.GetString() : null;
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(title))
            {
                continue;
            }

            var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            list.Add(new WebSearchResult { Title = title, Url = url, Snippet = snippet });
        }

        return list;
    }
}
