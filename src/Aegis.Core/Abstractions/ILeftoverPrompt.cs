using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Показывает окно со списком найденных остатков программы и спрашивает, что удалить (в духе Revo).
/// Реализуется в UI-слое. Возвращает выбранные пользователем остатки (пусто — ничего не удалять).
/// </summary>
public interface ILeftoverPrompt
{
    /// <param name="fullyRemoved">
    /// true — программа реально удалена штатным деинсталлятором (это её остатки);
    /// false — деинсталлятор НЕ убрал программу до конца (нужна перезагрузка / это лаунчер) — предупредить, что
    /// удаление затронет ещё живую программу.
    /// </param>
    Task<IReadOnlyList<LeftoverItem>> ConfirmAsync(
        string programName, IReadOnlyList<LeftoverItem> found, bool fullyRemoved = true);
}
