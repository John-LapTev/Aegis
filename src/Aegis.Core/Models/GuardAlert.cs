namespace Aegis.Core.Models;

/// <summary>
/// Предупреждение фонового стража — то, о чём Aegis сообщает пользователю, пока работает в фоне
/// (например, замечен возможный скрытый майнер). Короткое, на понятном языке.
/// </summary>
public sealed record GuardAlert
{
    /// <summary>Насколько серьёзно (обычно <see cref="Severity.Danger"/> — иначе стража бы не побеспокоила).</summary>
    public required Severity Severity { get; init; }

    /// <summary>Ключ для защиты от повторов: об одном и том же не сообщаем снова и снова.</summary>
    public required string Key { get; init; }

    /// <summary>Заголовок уведомления.</summary>
    public required string Title { get; init; }

    /// <summary>Текст уведомления простыми словами.</summary>
    public required string Message { get; init; }
}
