namespace Aegis.Core.Abstractions;

/// <summary>
/// Измеряет, сколько места занимают указанные файлы и папки ПРЯМО СЕЙЧАС. Нужен, чтобы цифры в списке не
/// «замерзали» на момент проверки: после чистки (в том числе частичной или сделанной вручную в проводнике)
/// размер пересчитывается и человек видит, сколько осталось на самом деле.
/// </summary>
public interface IPathSizeService
{
    /// <summary>
    /// Суммарный размер путей в байтах. Несуществующие пути считаются нулём — это и есть признак того, что
    /// папку уже почистили.
    /// </summary>
    Task<long> MeasureAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);
}
