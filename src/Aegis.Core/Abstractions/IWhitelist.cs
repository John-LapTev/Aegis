namespace Aegis.Core.Abstractions;

/// <summary>
/// Белый список «помечено как безопасное» — то, что пользователь сознательно одобрил. Помеченное
/// показывается зелёным (не скрывается) и помнится между запусками; пометку можно снять.
/// Ключ — стабильный признак (путь к файлу или Id находки).
/// </summary>
public interface IWhitelist
{
    /// <summary>Помечен ли ключ как безопасный.</summary>
    bool Contains(string key);

    /// <summary>Пометить ключ как безопасный (сохраняется между запусками).</summary>
    void Add(string key);

    /// <summary>Снять пометку «безопасно» (отмена).</summary>
    void Remove(string key);
}
