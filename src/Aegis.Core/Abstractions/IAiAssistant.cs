using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// ИИ-помощник (Gemini): отвечает простыми словами на вопросы об устройствах, файлах и процессах — там, где
/// офлайн-базы и VirusTotal молчат. Опционален (ключ из окружения/.personal); без ключа <see cref="IsConfigured"/>
/// = false. Совет ИИ — ПОДСКАЗКА, а не команда: решение и действие остаются за пользователем.
/// </summary>
public interface IAiAssistant
{
    /// <summary>Имя модели/провайдера (Gemini, Groq, Mistral…) — для показа активной модели в статусе.</summary>
    string Name { get; }

    /// <summary>Настроен ли помощник (есть рабочий ключ). Если нет — UI с ИИ просто не показываем.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Задать вопрос и получить ответ простыми словами (или понятную причину неудачи).
    /// <paramref name="webQuery"/> — запрос для реального веб-поиска (драйверы/утилиты/процессы): используется
    /// обёрткой <c>WebAugmentedAiAssistant</c>, чтобы подмешать свежие результаты; обычные клиенты его игнорируют.
    /// </summary>
    Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default);
}
