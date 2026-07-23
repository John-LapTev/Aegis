namespace Aegis.Scanners.Probing;

/// <summary>
/// Когда диски оптимизировались в последний раз. Windows делает это по расписанию сама, но расписание часто
/// оказывается отключённым (в том числе чужими «оптимизаторами»), и тогда SSD со временем пишет медленнее,
/// а обычный жёсткий диск — сильнее фрагментируется. Только читает.
/// </summary>
public interface IDiskOptimizeProbe
{
    Task<DiskOptimizeState> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Состояние обслуживания дисков.</summary>
public sealed record DiskOptimizeState
{
    /// <summary>Сколько дней прошло с последней оптимизации (null — узнать не удалось).</summary>
    public int? DaysSinceLastRun { get; init; }

    /// <summary>Включено ли автоматическое обслуживание дисков по расписанию.</summary>
    public bool ScheduleEnabled { get; init; }

    /// <summary>Есть ли в компьютере твердотельный диск (для правильного объяснения).</summary>
    public bool HasSolidStateDrive { get; init; }
}
