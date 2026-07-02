namespace Aegis.Scanners.Probing;

/// <summary>Снимок «залежавшегося»: битые ярлыки, пустые файлы, давно не тронутые загрузки.</summary>
public sealed record StaleFileSnapshot
{
    public required IReadOnlyList<StaleFile> Items { get; init; }
}

/// <summary>Один «залежавшийся» элемент (файл/ярлык).</summary>
public sealed record StaleFile
{
    /// <summary>Понятный заголовок.</summary>
    public required string Title { get; init; }

    /// <summary>Полный путь.</summary>
    public required string Path { get; init; }

    /// <summary>Тип — определяет секцию, текст и безопасность массовой очистки.</summary>
    public required StaleFileKind Kind { get; init; }

    /// <summary>Доп. деталь (например, «не менялся 120 дней»). Необязательно.</summary>
    public string? Note { get; init; }
}

/// <summary>Тип «залежавшегося».</summary>
public enum StaleFileKind
{
    /// <summary>Ярлык, цель которого больше не существует (ведёт в никуда) — безопасно убрать.</summary>
    BrokenShortcut,

    /// <summary>Файл нулевого размера — пустышка, безопасно убрать.</summary>
    EmptyFile,

    /// <summary>Файл в «Загрузках», который давно не менялся — осторожно (вдруг нужен), по одному.</summary>
    OldDownload,
}
