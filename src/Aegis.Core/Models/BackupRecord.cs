namespace Aegis.Core.Models;

/// <summary>
/// Запись о созданном бэкапе — отображается в разделе «Бэкапы» и используется для отката.
/// Создаётся ПЕРЕД каждой правкой (ADR 0002, 0004).
/// </summary>
public sealed record BackupRecord
{
    /// <summary>Идентификатор бэкапа (для отката и логов).</summary>
    public required string Id { get; init; }

    /// <summary>Вид бэкапа.</summary>
    public required BackupKind Kind { get; init; }

    /// <summary>Человеческое описание: перед чем сделан (например, «Перед починкой автозапуска»).</summary>
    public required string Description { get; init; }

    /// <summary>Момент создания.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Что затронуто (реестр, автозапуск, настройки…). Показывается тегами в UI.</summary>
    public required IReadOnlyList<string> AffectedAreas { get; init; }

    /// <summary>Размер бэкапа в байтах (для отображения; 0 если неизвестно).</summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Реально ли создан этот бэкап. Для точки восстановления Windows = false, если System Restore
    /// выключена/недоступна (тогда обратимость держится на точечных бэкапах правок, а не на «зонтике»).
    /// По умолчанию true — точечные бэкапы (экспорт ветки, карантин) либо создаются, либо правка отменяется.
    /// </summary>
    public bool Succeeded { get; init; } = true;
}
