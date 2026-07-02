namespace Aegis.Scanners.Probing;

/// <summary>Снимок «остатков» удалённых программ/игр: папки в профиле, которым, возможно, уже нечего обслуживать.</summary>
public sealed record LeftoverSnapshot
{
    /// <summary>Папки-кандидаты на остатки (из %AppData%/%LocalAppData% и т.п.).</summary>
    public required IReadOnlyList<LeftoverFolder> Folders { get; init; }
}

/// <summary>Папка-кандидат на «остаток» удалённой программы (read-only описание для классификации).</summary>
public sealed record LeftoverFolder
{
    /// <summary>Имя папки (обычно вендор/название программы).</summary>
    public required string Name { get; init; }

    /// <summary>Полный путь к папке.</summary>
    public required string Path { get; init; }

    /// <summary>Размер папки в байтах (0 для пустой).</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Папка пуста (внутри нет ни файлов, ни подпапок — гарантированно) — однозначно безопасно убрать.</summary>
    public required bool IsEmpty { get; init; }

    /// <summary>Имя папки совпало с установленной программой/издателем — значит, программа ещё стоит (НЕ остаток).</summary>
    public required bool MatchesInstalled { get; init; }

    /// <summary>Папку недавно меняли (активно используется) — НЕ предлагать как остаток, даже если непустая.</summary>
    public required bool RecentlyUsed { get; init; }
}
