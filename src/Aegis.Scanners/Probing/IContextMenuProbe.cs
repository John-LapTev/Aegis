namespace Aegis.Scanners.Probing;

/// <summary>
/// Чтение пунктов контекстного меню проводника (правый клик). Только читает.
/// Реализация Windows-специфична.
/// </summary>
public interface IContextMenuProbe
{
    /// <summary>Пункты меню, которые ссылаются на несуществующие программы.</summary>
    Task<IReadOnlyList<ContextMenuEntry>> ReadBrokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>Пункт контекстного меню, оставшийся от удалённой программы.</summary>
public sealed record ContextMenuEntry
{
    /// <summary>Название пункта, как его видит человек.</summary>
    public required string Name { get; init; }

    /// <summary>Где появляется: «на файлах», «на папках», «на пустом месте в папке».</summary>
    public required string Scope { get; init; }

    /// <summary>Куда ведёт (путь к исчезнувшей программе) — для показа.</summary>
    public string? Target { get; init; }

    /// <summary>Куст реестра значения, которым пункт отключается.</summary>
    public required string Hive { get; init; }

    /// <summary>Путь ключа реестра для отключения.</summary>
    public required string SubKey { get; init; }

    /// <summary>Имя значения для отключения (<c>LegacyDisable</c> либо CLSID в списке заблокированных).</summary>
    public required string ValueName { get; init; }
}
