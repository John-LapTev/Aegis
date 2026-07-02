namespace Aegis.Scanners.Probing;

/// <summary>Снимок здоровья батареи (для ноутбуков). На стационарном ПК батареи нет.</summary>
public sealed record BatterySnapshot
{
    /// <summary>Есть ли в системе батарея.</summary>
    public required bool HasBattery { get; init; }

    /// <summary>Износ батареи в процентах (0 — как новая; null — не удалось измерить).</summary>
    public int? WearPercent { get; init; }

    /// <summary>Заводская ёмкость, мВт·ч (если известна).</summary>
    public int? DesignedCapacity { get; init; }

    /// <summary>Текущая полная ёмкость, мВт·ч (если известна).</summary>
    public int? FullChargedCapacity { get; init; }

    /// <summary>Сколько раз батарею заряжали (циклы), если датчик отдаёт; null — недоступно.</summary>
    public int? CycleCount { get; init; }

    /// <summary>Текущий заряд, % (null — неизвестно).</summary>
    public int? ChargePercent { get; init; }

    /// <summary>Заряжается ли сейчас (подключена зарядка). null — неизвестно.</summary>
    public bool? IsCharging { get; init; }

    /// <summary>Сколько минут работы осталось от батареи (при разрядке; null — неизвестно/на зарядке).</summary>
    public int? RemainingMinutes { get; init; }
}
