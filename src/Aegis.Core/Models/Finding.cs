namespace Aegis.Core.Models;

/// <summary>
/// Одна находка сканера = строка в списке результатов.
/// Содержит техническую суть и «?»-объяснение простыми словами для пользователя.
/// </summary>
public sealed record Finding
{
    /// <summary>Стабильный идентификатор находки (для выделения, дедупликации, логов).</summary>
    public required string Id { get; init; }

    /// <summary>Группа (вкладка), к которой относится находка.</summary>
    public required ScanGroup Group { get; init; }

    /// <summary>Важность — определяет бейдж статуса.</summary>
    public required Severity Severity { get; init; }

    /// <summary>Короткий заголовок находки (понятный человеку).</summary>
    public required string Title { get; init; }

    /// <summary>
    /// «?»-объяснение простыми словами: в чём суть проблемы и что даст исправление и зачем.
    /// Показывается в тултипе по значку «?».
    /// </summary>
    public required string Explain { get; init; }

    /// <summary>
    /// Техническая деталь (путь к файлу, ключ реестра, имя процесса, хэш) —
    /// показывается моноширинным шрифтом. Необязательна.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Структурированные параметры для исправления (например, пути для очистки мусора).
    /// Заполняется сканером, читается фабрикой правок. Необязательно.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Data { get; init; }
}
