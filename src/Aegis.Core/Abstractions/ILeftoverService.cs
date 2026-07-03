using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Ищет и удаляет «хвосты» после удаления программы (папки, файлы, ветки реестра) — для окна остатков в духе Revo.
/// Поиск НИЧЕГО не удаляет; удаление — только по явному выбору пользователя и обратимо (Корзина + бэкап реестра).
/// </summary>
public interface ILeftoverService
{
    /// <summary>Найти остатки удалённой программы (без удаления).</summary>
    Task<IReadOnlyList<LeftoverItem>> ScanAsync(InstalledProgram program, CancellationToken cancellationToken = default);

    /// <summary>Удалить выбранные остатки (папки/файлы — в Корзину, реестр — с бэкапом). Возвращает число удалённых.</summary>
    Task<int> RemoveAsync(IReadOnlyList<LeftoverItem> items, CancellationToken cancellationToken = default);
}
