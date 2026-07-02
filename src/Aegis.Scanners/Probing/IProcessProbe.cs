namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик списка запущенных процессов. Только ЧИТАЕТ. Windows-реализация — в слое доступа
/// к системе; логика <see cref="Processes.ProcessesScanner"/> работает поверх этой абстракции.
/// </summary>
public interface IProcessProbe
{
    /// <summary>Перечислить запущенные процессы с метаданными.</summary>
    Task<IReadOnlyList<ProcessInfo>> FindAsync(CancellationToken cancellationToken = default);
}
