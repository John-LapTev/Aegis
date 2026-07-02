using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Планировщик «отката после перезагрузки» для рискованных правок. После важного изменения системы записывает
/// автозапуск (RunOnce) + памятку: при следующем входе программа спросит «всё работает?», и если пользователь
/// НЕ подтвердит — откатит сделанные правки по их бэкапам. Реализация — Windows-специфична (RunOnce/реестр).
/// </summary>
public interface IRebootRollbackScheduler
{
    /// <summary>Запланировать проверку после перезагрузки: при отказе откатываем правки по <paramref name="backupIds"/>.</summary>
    void Schedule(IReadOnlyList<string> backupIds, string description);

    /// <summary>Есть ли незакрытая проверка отката (читаем при запуске программы); null — нет.</summary>
    PendingRollback? GetPending();

    /// <summary>Снять запланированную проверку (пользователь подтвердил «всё ок» или откат уже выполнен).</summary>
    void Clear();
}
