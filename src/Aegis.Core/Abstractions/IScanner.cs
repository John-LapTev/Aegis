using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Плагин-сканер для одной группы. Сканеры регистрируются в DI и запускаются
/// оркестратором; каждый возвращает <see cref="ScanResult"/> со своими находками.
/// Реализации не меняют систему — только читают и анализируют (правки — отдельный слой Fix).
/// </summary>
public interface IScanner
{
    /// <summary>Группа (вкладка), которую обслуживает сканер.</summary>
    ScanGroup Group { get; }

    /// <summary>
    /// Выполнить сканирование. Должно быть асинхронным и не блокировать UI-поток;
    /// поддерживать отмену через <paramref name="cancellationToken"/>.
    /// </summary>
    Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default);
}
