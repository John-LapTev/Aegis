using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Применяет пакет исправлений безопасно и обратимо: создаёт точку восстановления ПЕРЕД пакетом,
/// затем применяет каждое исправление (по одному или массово «исправить выбранное»).
/// Если бэкап пакета создать не удалось — изменения НЕ вносятся (ADR 0002, 0004).
/// </summary>
public interface IFixOrchestrator
{
    /// <summary>
    /// Применить набор исправлений. <paramref name="batchDescription"/> попадёт в описание точки
    /// восстановления (например, «Перед починкой автозапуска (3 пункта)»).
    /// </summary>
    Task<BatchFixResult> ApplyAsync(
        IReadOnlyList<IFix> fixes,
        string batchDescription,
        IProgress<FixProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
