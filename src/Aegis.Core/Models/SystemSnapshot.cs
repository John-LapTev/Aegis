namespace Aegis.Core.Models;

/// <summary>
/// Снимок ключевого состояния системы для функции «Что изменилось»: списки идентификаторов автозапуска,
/// установленных программ и записей файла hosts. Сравнивая снимок с прошлым, показываем, что ПОЯВИЛОСЬ нового
/// (тихо установившиеся программы, новые элементы автозапуска, правки hosts) — это ловит вирусы и «попутный» софт.
/// </summary>
public sealed record SystemSnapshot
{
    /// <summary>Когда снят (UTC).</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Идентификаторы элементов автозапуска.</summary>
    public required IReadOnlyList<string> Autostart { get; init; }

    /// <summary>Названия установленных программ.</summary>
    public required IReadOnlyList<string> Programs { get; init; }

    /// <summary>Записи файла hosts (домен → адрес).</summary>
    public required IReadOnlyList<string> HostsEntries { get; init; }
}
