using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Честная «оптимизация памяти»: показывает реальную занятость ОЗУ и список ФОНОВЫХ процессов (обновляторы,
/// помощники), которые можно безопасно закрыть, чтобы временно освободить память. Без липовых цифр и без
/// закрытия важных/пользовательских программ. Реализация Windows-специфична.
/// </summary>
public interface IMemoryOptimizer
{
    /// <summary>Текущее состояние: снимок памяти + безопасно-закрываемые фоновые процессы.</summary>
    Task<MemoryOptimizerState> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Закрыть выбранные процессы по PID. Возвращает, сколько реально закрыто.</summary>
    Task<int> StopAsync(IReadOnlyList<int> processIds, CancellationToken cancellationToken = default);
}
