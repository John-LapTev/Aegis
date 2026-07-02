namespace Aegis.Core.Models;

/// <summary>Итог применения пакета исправлений (одиночного или массового «исправить выбранное»).</summary>
public sealed record BatchFixResult
{
    /// <summary>Результаты по каждому исправлению (в порядке применения).</summary>
    public required IReadOnlyList<FixOutcome> Outcomes { get; init; }

    /// <summary>Нужна ли перезагрузка, чтобы изменения вступили в силу (если хотя бы одно требует).</summary>
    public required bool RequiresReboot { get; init; }

    /// <summary>Идентификатор точки восстановления, созданной ПЕРЕД пакетом (для отката всего пакета).</summary>
    public string? RestorePointId { get; init; }

    /// <summary>
    /// Пакет прерван ДО внесения изменений (например, не удалось создать точку восстановления) —
    /// ради безопасности ничего не правили.
    /// </summary>
    public bool Aborted { get; init; }

    /// <summary>Понятное пользователю сообщение (при прерывании/ошибке) — на русском.</summary>
    public string? Message { get; init; }

    /// <summary>Сколько исправлений применено успешно.</summary>
    public int SuccessCount => Outcomes.Count(static o => o.Success);

    /// <summary>Сколько исправлений завершилось ошибкой.</summary>
    public int FailureCount => Outcomes.Count(static o => !o.Success);
}
