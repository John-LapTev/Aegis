namespace Aegis.Core.Models;

/// <summary>
/// Степень важности находки. Определяет цвет и текст бейджа статуса в UI
/// (см. семантику статусов в docs/DESIGN.md).
/// </summary>
public enum Severity
{
    /// <summary>Всё в порядке или уже исправлено.</summary>
    Ok,

    /// <summary>Нейтральная информация, действие не требуется.</summary>
    Info,

    /// <summary>Предупреждение — некритично, но стоит обратить внимание.</summary>
    Warning,

    /// <summary>Проблема или угроза — требует внимания.</summary>
    Danger,
}
