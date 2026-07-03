namespace Aegis.Core.Models;

/// <summary>Что это за остаток удалённой программы — для понятной иконки/подписи и способа удаления.</summary>
public enum LeftoverKind
{
    /// <summary>Папка на диске.</summary>
    Folder,

    /// <summary>Отдельный файл.</summary>
    File,

    /// <summary>Ветка реестра.</summary>
    RegistryKey,

    /// <summary>Отдельное значение в ветке реестра (например, запись автозапуска в ...\Run).</summary>
    RegistryValue,
}

/// <summary>
/// Один «хвост», оставшийся после удаления программы: папка/файл на диске или ветка реестра.
/// Показывается пользователю в окне после удаления, чтобы он сам решил, что вычистить (в духе Revo).
/// </summary>
public sealed record LeftoverItem
{
    /// <summary>Что это (папка/файл/реестр).</summary>
    public required LeftoverKind Kind { get; init; }

    /// <summary>Путь к файлу/папке или путь ветки реестра (например, «HKLM\SOFTWARE\…»).</summary>
    public required string Path { get; init; }

    /// <summary>Понятная подпись для человека.</summary>
    public required string Display { get; init; }

    /// <summary>Имя значения в ветке реестра (только для <see cref="LeftoverKind.RegistryValue"/>).</summary>
    public string? ValueName { get; init; }

    /// <summary>Размер (для файлов/папок), байт; 0 для реестра/неизвестно.</summary>
    public long SizeBytes { get; init; }
}
