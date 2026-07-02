using System.Net;
using System.Text;
using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Ai;

/// <summary>
/// Клиент Gemini API (модель Flash): задаёт вопрос и возвращает текстовый ответ. Ключ — из окружения/.personal,
/// никогда в репозиторий. Бесплатный тариф ограничен по лимиту (429) — обрабатываем мягко: говорим «ИИ временно
/// недоступен», не роняя приложение. HttpClient внедряется снаружи (тестируется фейковым обработчиком).
/// </summary>
public sealed class GeminiClient : IAiAssistant
{
    private const string Endpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiClient(HttpClient httpClient, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public string Name => "Gemini";

    public bool IsConfigured => true;

    public async Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        // Свой таймаут на запрос, чтобы цепочка моделей не висла ~100с (таймаут HttpClient) на каждой.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey); // ключ в заголовке, не в URL (не утечёт в логи)

            using var response = await _httpClient.SendAsync(request, linked.Token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var errorBody = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
                var retry = ParseRetryAfter(errorBody);
                var message = retry is null
                    ? "Лимит бесплатного тарифа Gemini исчерпан — ИИ временно недоступен."
                    : $"Лимит исчерпан. ИИ снова заработает примерно через {retry}.";
                return AiResult.Limit(message, retry);
            }

            if (!response.IsSuccessStatusCode)
            {
                return AiResult.Fail("ИИ-помощник недоступен (ошибка сервиса). Попробуйте позже.");
            }

            var json = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            var text = ExtractText(json);
            return text is { Length: > 0 }
                ? AiResult.Ok(text)
                : AiResult.Fail("ИИ-помощник не дал ответа.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // настоящую отмену вызывающего пробрасываем (не подменяем «нет интернета»)
        }
        catch (Exception)
        {
            return AiResult.Fail("Нет связи с ИИ-помощником — проверьте интернет.");
        }
    }

    /// <summary>Из тела ответа 429 достать время до сброса лимита (RetryInfo.retryDelay), по-русски; null если нет.</summary>
    public static string? ParseRetryAfter(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("error", out var error)
                || !error.TryGetProperty("details", out var details))
            {
                return null;
            }

            string? retryDelay = null;
            var dailyExhausted = false;
            foreach (var detail in details.EnumerateArray())
            {
                // RetryInfo.retryDelay — это ПОМИНУТНЫЙ бэкофф; для дневного лимита он обманчив (покажет «59 сек»).
                if (detail.TryGetProperty("retryDelay", out var rd) && rd.GetString() is { Length: > 0 } delay)
                {
                    retryDelay = delay;
                }

                // QuotaFailure: если исчерпан ДНЕВНОЙ лимит (quotaId с «PerDay») — сбросится только на следующий день.
                if (detail.TryGetProperty("violations", out var violations) && violations.ValueKind == JsonValueKind.Array)
                {
                    foreach (var violation in violations.EnumerateArray())
                    {
                        if (violation.TryGetProperty("quotaId", out var q)
                            && q.GetString() is { } quotaId
                            && quotaId.Contains("PerDay", StringComparison.OrdinalIgnoreCase))
                        {
                            dailyExhausted = true;
                        }
                    }
                }
            }

            if (dailyExhausted)
            {
                return "лимит на сегодня, обновится завтра";
            }

            return retryDelay is not null ? FormatDelay(retryDelay) : null;
        }
        catch (Exception)
        {
            // Тело без RetryInfo/QuotaFailure — времени не знаем.
        }

        return null;
    }

    /// <summary>«30s» → «30 секунд», «90s» → «1.5 минуты» и т.п. (для подсказки в плашке статуса).</summary>
    private static string FormatDelay(string delay)
    {
        var digits = delay.TrimEnd('s', 'm', 'h', ' ');
        if (!double.TryParse(digits, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return delay;
        }

        var seconds = delay.TrimEnd(' ') switch
        {
            var d when d.EndsWith('m') => value * 60,
            var d when d.EndsWith('h') => value * 3600,
            _ => value,
        };

        return seconds < 60
            ? $"{Math.Ceiling(seconds):0} сек"
            : $"{Math.Ceiling(seconds / 60):0} мин";
    }

    /// <summary>Достать текст из ответа Gemini: candidates[0].content.parts[0].text (чистая функция — для тестов).</summary>
    public static string? ExtractText(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            if (parts.GetArrayLength() == 0)
            {
                return null;
            }

            return parts[0].GetProperty("text").GetString()?.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
