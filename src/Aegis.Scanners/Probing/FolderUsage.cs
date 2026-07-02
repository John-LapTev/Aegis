namespace Aegis.Scanners.Probing;

/// <summary>Папка и её размер — для обзора «что занимает место на диске».</summary>
public sealed record FolderUsage
{
    /// <summary>Полный путь к папке.</summary>
    public required string Path { get; init; }

    /// <summary>Суммарный размер содержимого в байтах.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Известная папка профиля (Загрузки, Рабочий стол…) для подписи простыми словами; по умолчанию обычная.</summary>
    public UserFolderKind Kind { get; init; } = UserFolderKind.Other;

    /// <summary>Крупнейшие элементы прямо внутри папки (файлы и подпапки) — для раскрывающегося списка; пусто, если не собирали.</summary>
    public IReadOnlyList<FolderEntry> Children { get; init; } = [];
}
