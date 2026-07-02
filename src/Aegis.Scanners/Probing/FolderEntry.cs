namespace Aegis.Scanners.Probing;

/// <summary>
/// Один элемент внутри большой папки (файл или вложенная папка) — для раскрывающегося списка содержимого:
/// показать имя, размер, тип (по нему рисуется иконка) и дать удалить выбранное. Размер вложенной папки —
/// суммарный по всему её содержимому.
/// </summary>
public sealed record FolderEntry
{
    /// <summary>Имя файла/папки (без пути).</summary>
    public required string Name { get; init; }

    /// <summary>Полный путь — для открытия и удаления.</summary>
    public required string Path { get; init; }

    /// <summary>Размер в байтах (для папки — суммарный по содержимому).</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Это вложенная папка (true) или файл (false).</summary>
    public bool IsDirectory { get; init; }
}
