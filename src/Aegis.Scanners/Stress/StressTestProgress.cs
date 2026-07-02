namespace Aegis.Scanners.Stress;

/// <summary>Живое состояние проверки под нагрузкой — для шкалы и цифр на экране (обновляется каждый шаг).</summary>
public sealed record StressTestProgress
{
    /// <summary>Сколько секунд прошло.</summary>
    public required int ElapsedSeconds { get; init; }

    /// <summary>Сколько секунд запланировано всего.</summary>
    public required int PlannedSeconds { get; init; }

    /// <summary>Текущая температура процессора, °C (null — датчик недоступен).</summary>
    public int? CpuCelsius { get; init; }

    /// <summary>Текущая температура видеокарты, °C (null — датчик недоступен).</summary>
    public int? GpuCelsius { get; init; }

    /// <summary>Максимум температуры процессора за тест, °C.</summary>
    public int? MaxCpuCelsius { get; init; }

    /// <summary>Максимум температуры видеокарты за тест, °C.</summary>
    public int? MaxGpuCelsius { get; init; }

    /// <summary>Доля выполнения 0..1 — для шкалы прогресса.</summary>
    public double Fraction => PlannedSeconds <= 0 ? 0 : Math.Min(1.0, (double)ElapsedSeconds / PlannedSeconds);
}
