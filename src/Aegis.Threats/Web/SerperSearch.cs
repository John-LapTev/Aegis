using System.Net.Http;
using System.Text;
using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Web;

/// <summary>
/// Веб-поиск через Serper.dev (выдача Google как API). Бесплатный стартовый лимит (~2500 запросов), БЕЗ карты.
/// Используется как запасной к Tavily. Best-effort: при ошибке/лимите → пустой список (откат на следующий поиск).
/// </summary>
public sealed class SerperSearch : IWebSearch
{
    private const string Endpoint = "https://google.serper.dev/search";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public SerperSearch(HttpClient http, string apiKey)
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

            var payload = JsonSerializer.Serialize(new { q = query, num = Math.Clamp(maxResults, 1, 20) });
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("X-API-KEY", _apiKey);

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

    /// <summary>Разбирает JSON Serper (organic[].{title,link,snippet}) в наши результаты.</summary>
    public static IReadOnlyList<WebSearchResult> Parse(string json, int maxResults)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("organic", out var organic) || organic.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<WebSearchResult>();
        foreach (var item in organic.EnumerateArray())
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
