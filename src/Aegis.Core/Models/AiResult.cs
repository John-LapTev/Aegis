namespace Aegis.Core.Models;

/// <summary>Результат запроса к ИИ-помощнику (Gemini): либо текст ответа, либо понятная причина неудачи для UI.</summary>
public sealed record AiResult
{
    /// <summary>Успешно ли получен ответ.</summary>
    public required bool Success { get; init; }

    /// <summary>Ответ ИИ простыми словами (если <see cref="Success"/>).</summary>
    public string? Text { get; init; }

    /// <summary>Понятная причина, если не получилось (лимит/нет ключа/нет сети) — показываем пользователю.</summary>
    public string? Error { get; init; }

    /// <summary>true — неудача именно из-за исчерпанного лимита (429), а не сети/ключа.</summary>
    public bool LimitReached { get; init; }

    /// <summary>Через сколько ИИ снова станет доступен (из ответа сервиса), напр. «30 секунд»; null — неизвестно.</summary>
    public string? RetryAfter { get; init; }

    /// <summary>Какая модель ответила (Gemini/Groq/Mistral…) — для показа активной модели в статусе.</summary>
    public string? Provider { get; init; }

    public static AiResult Ok(string text) => new() { Success = true, Text = text };

    public static AiResult Fail(string error) => new() { Success = false, Error = error };

    public static AiResult Limit(string error, string? retryAfter) =>
        new() { Success = false, Error = error, LimitReached = true, RetryAfter = retryAfter };
}
