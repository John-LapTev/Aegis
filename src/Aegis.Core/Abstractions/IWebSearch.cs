using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Реальный веб-поиск (драйверы, утилиты, процессы) — чтобы ИИ отвечал не «по памяти», а по свежим данным
/// из интернета со ссылками. Любая модель (Gemini/Groq/Mistral) сама в сеть не ходит: поиск делаем мы и
/// отдаём результаты модели как контекст. Best-effort: при сбое/без сети возвращаем пустой список.
/// </summary>
public interface IWebSearch
{
    /// <summary>Найти по запросу; вернуть несколько верхних результатов (или пусто, если не вышло).</summary>
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
}
