using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Хранилище истории здоровья дисков для функции «раннее предупреждение по трендам».
/// Хранит скользящее окно последних снимков (см. реализацию), чтобы сравнивать динамику.
/// </summary>
public interface IHealthTrendStore
{
    /// <summary>История снимков от старых к новым (пусто, если ещё ничего не копилось).</summary>
    IReadOnlyList<HealthTrendSnapshot> LoadHistory();

    /// <summary>Добавляет новый снимок в историю (старые сверх лимита отбрасываются).</summary>
    void Append(HealthTrendSnapshot snapshot);
}
