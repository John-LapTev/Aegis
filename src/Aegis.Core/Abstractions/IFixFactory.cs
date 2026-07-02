using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Сопоставляет находку с конкретным обратимым исправлением (<see cref="IFix"/>).
/// Реализация — Windows-специфична (правки реестра/служб/файлов) и живёт в слое доступа к системе.
/// </summary>
public interface IFixFactory
{
    /// <summary>Есть ли для находки готовое исправление.</summary>
    bool CanFix(Finding finding);

    /// <summary>
    /// Создать исправление для находки или null, если оно не предусмотрено. Для правок-удалений
    /// <paramref name="permanentDelete"/>=true означает «удалить навсегда» (пользователь подтвердил),
    /// иначе — в Корзину (обратимо). Для прочих правок флаг игнорируется.
    /// </summary>
    IFix? CreateFix(Finding finding, bool permanentDelete = false);
}
