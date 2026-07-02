using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Ai;

/// <summary>
/// Цепочка ИИ-моделей с автоматическим переключением: спрашиваем по порядку (Gemini → ChatGPT → Claude). Первая,
/// что ответит — её результат. Если у модели исчерпан лимит (или ошибка) — переходим к следующей. Когда лимит
/// первой обновится, снова начинаем с неё (порядок фиксированный). Платная модель (Claude) — последняя, только
/// когда бесплатные не ответили. Так выжимаем максимум из бесплатных тарифов.
/// </summary>
public sealed class FallbackAiAssistant : IAiAssistant
{
    private readonly IReadOnlyList<IAiAssistant> _providers;

    public FallbackAiAssistant(IReadOnlyList<IAiAssistant> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.Where(p => p.IsConfigured).ToList();
    }

    public string Name => _providers.Count > 0 ? _providers[0].Name : "—";

    public bool IsConfigured => _providers.Count > 0;

    /// <summary>Модели цепочки по порядку — чтобы раздел «Нейросети» мог показать статус каждой отдельно.</summary>
    public IReadOnlyList<IAiAssistant> Providers => _providers;

    public async Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default)
    {
        if (_providers.Count == 0)
        {
            return AiResult.Fail("ИИ-помощник не настроен.");
        }

        AiResult? last = null;

        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.AskAsync(prompt, webQuery, cancellationToken).ConfigureAwait(false);
                last = result with { Provider = provider.Name };
                if (result.Success)
                {
                    return last;
                }
                // лимит/ошибка → пробуем следующую модель
            }
            catch (OperationCanceledException)
            {
                throw; // отмену пользователя пробрасываем, не глотаем
            }
            catch (Exception ex)
            {
                // Провайдер бросил неожиданное исключение — не обрываем всю цепочку, пробуем следующую модель.
                last = AiResult.Fail($"{provider.Name}: {ex.Message}") with { Provider = provider.Name };
            }
        }

        // Ни одна модель не ответила — возвращаем результат последней как есть: её флаги
        // (LimitReached/Error) точны и не противоречат тексту (правка аудита).
        return last ?? AiResult.Fail("ИИ недоступен.");
    }
}
