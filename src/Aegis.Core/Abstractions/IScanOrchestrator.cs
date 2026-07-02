using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Запускает зарегистрированные <see cref="IScanner"/> и собирает результаты по группам,
/// сообщая прогресс. Точка входа сканирования для дашборда (Фаза 1–2).
/// </summary>
public interface IScanOrchestrator
{
    /// <summary>
    /// Просканировать все доступные группы. <paramref name="progress"/> получает обновления
    /// по мере прохождения групп; отмена — через <paramref name="cancellationToken"/>.
    /// </summary>
    Task<IReadOnlyList<ScanResult>> ScanAllAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
