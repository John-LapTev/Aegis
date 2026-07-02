using System.Net.Http;
using System.Text;
using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Web;

/// <summary>
/// Веб-поиск через Tavily Search API — поисковик, СДЕЛАННЫЙ ДЛЯ ИИ: отдаёт чистые результаты (заголовок, ссылка,
/// текст) под обработку языковой моделью. Бесплатный тариф ~1000 поисков/мес, БЕЗ карты. Основной провайдер,
/// когда задан ключ (tvly-…); иначе DuckDuckGo. Best-effort: при ошибке/лимите → пустой список (откат на запасной).
/// </summary>
public sealed class TavilySearch : IWebSearch
{
    private const string Endpoint = "https://api.tavily.com/search";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public TavilySearch(HttpClient http, string apiKey)
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
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var payload = JsonSerializer.Serialize(new
            {
                api_key = _apiKey,
                query,
                max_results = Math.Clamp(maxResults, 1, 20),
                search_depth = "basic",
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };

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

    /// <summary>Разбирает JSON Tavily (results[].{title,url,content}) в наши результаты.</summary>
    public static IReadOnlyList<WebSearchResult> Parse(string json, int maxResults)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
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

            var content = item.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            list.Add(new WebSearchResult { Title = title, Url = url, Snippet = content });
        }

        return list;
    }
}
