namespace Aegis.Scanners.Probing;

/// <summary>
/// Постоянная подписка WMI (root\subscription) — редкий механизм автозапуска: при событии WMI выполняет
/// команду/скрипт. Им часто пользуется скрытая малварь (в т.ч. майнеры), почти не используется обычными
/// программами. Read-only — что нашли, классифицирует сканер.
/// </summary>
public sealed record WmiPersistence
{
    /// <summary>Имя «потребителя» (EventConsumer).</summary>
    public required string Name { get; init; }

    /// <summary>Тип: «командная строка» (CommandLineEventConsumer) или «скрипт» (ActiveScriptEventConsumer).</summary>
    public required string Kind { get; init; }

    /// <summary>Что выполняется — команда или текст скрипта (для показа и эвристики).</summary>
    public required string Action { get; init; }
}
