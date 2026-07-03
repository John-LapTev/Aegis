using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Читает реальные измерения скорости загрузки Windows (журнал «Diagnostics-Performance»).</summary>
public interface IBootPerformanceProbe
{
    /// <summary>Возвращает время загрузки и список тормозящих её элементов (пустой результат — если данных ещё нет).</summary>
    Task<BootPerformance> ReadAsync(CancellationToken cancellationToken = default);
}
