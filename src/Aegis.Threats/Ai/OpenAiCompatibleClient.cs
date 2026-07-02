using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Ai;

/// <summary>
/// Клиент к любой модели с OpenAI-совместимым API (Groq, Mistral, сам OpenAI…): POST /chat/completions с
/// Authorization: Bearer. Один класс на всех — отличаются только endpoint/модель/ключ/имя. Ключ — из .personal,
/// не в репозиторий. 429 → мягко «лимит исчерпан» (чтобы цепочка перешла к следующей модели).
/// </summary>
public sealed class OpenAiCompatibleClient : IAiAssistant
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly string _apiKey;

    public OpenAiCompatibleClient(HttpClient httpClient, string name, string endpoint, string model, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _httpClient = httpClient;
        Name = name;
        _endpoint = endpoint;
        _model = model;
        _apiKey = apiKey;
    }

    public string Name { get; }

    public bool IsConfigured => true;

    public async Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        // Свой таймаут, чтобы цепочка моделей не висла ~100с на каждой.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                model = _model,
                messages = new[] { new { role = "user", content = prompt } },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");

            using var response = await _httpClient.SendAsync(request, linked.Token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retry = FormatRetryAfter(response.Headers.RetryAfter?.Delta);
                return AiResult.Limit($"Лимит {Name} исчерпан.", retry);
            }

            if (!response.IsSuccessStatusCode)
            {
                return AiResult.Fail($"{Name} недоступен (ошибка сервиса).");
            }

            var json = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            var text = ExtractText(json);
            return text is { Length: > 0 }
                ? AiResult.Ok(text)
                : AiResult.Fail($"{Name} не дал ответа.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // настоящую отмену вызывающего пробрасываем
        }
        catch (Exception)
        {
            return AiResult.Fail($"Нет связи с {Name}.");
        }
    }

    /// <summary>Заголовок Retry-After (TimeSpan) → «N сек»/«N мин» для подсказки в статусе; null если нет.</summary>
    private static string? FormatRetryAfter(TimeSpan? delta)
    {
        if (delta is not { } span || span <= TimeSpan.Zero)
        {
            return null;
        }

        return span.TotalSeconds < 60
            ? $"{Math.Ceiling(span.TotalSeconds):0} сек"
            : $"{Math.Ceiling(span.TotalMinutes):0} мин";
    }

    /// <summary>Достать текст из OpenAI-ответа: choices[0].message.content (чистая функция — для тестов).</summary>
    public static string? ExtractText(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            return choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
