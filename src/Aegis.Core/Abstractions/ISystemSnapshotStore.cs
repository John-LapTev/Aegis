using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Хранит последний снимок состояния системы (для сравнения «что изменилось» между проверками).</summary>
public interface ISystemSnapshotStore
{
    /// <summary>Последний сохранённый снимок; null — снимка ещё нет (первая проверка).</summary>
    SystemSnapshot? LoadLatest();

    /// <summary>Сохранить снимок как новую точку отсчёта.</summary>
    void Save(SystemSnapshot snapshot);
}
