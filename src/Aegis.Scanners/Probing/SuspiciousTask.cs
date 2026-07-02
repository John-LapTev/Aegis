namespace Aegis.Scanners.Probing;

/// <summary>
/// Задача планировщика с подозрительной командой (закодированный запуск, скачивание, запуск из Temp/AppData) —
/// частый способ закрепиться у малвари/майнеров. Read-only — окончательно классифицирует сканер.
/// </summary>
public sealed record SuspiciousTask
{
    /// <summary>Полный путь задачи (\Папка\Имя) — нужен, чтобы её отключить.</summary>
    public required string Path { get; init; }

    /// <summary>Короткое имя задачи.</summary>
    public required string Name { get; init; }

    /// <summary>Команда задачи (программа + аргументы) — для показа и эвристики.</summary>
    public required string Action { get; init; }
}
