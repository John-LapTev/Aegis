namespace Aegis.Scanners.Stress;

/// <summary>
/// Настройки безопасной проверки под нагрузкой. Пороги авто-стопа подобраны так, чтобы НИКОГДА не доводить
/// до опасного нагрева: дошли до порога — тест останавливается сам. Длительность = и «сколько собираем
/// данные», и «таймер-предохранитель» (дольше тест не идёт).
/// </summary>
public sealed record StressTestOptions
{
    /// <summary>Длительность короткого безопасного прогрева, сек.</summary>
    public int SafeSeconds { get; init; } = 60;

    /// <summary>Длительность углублённой проверки, сек.</summary>
    public int DeepSeconds { get; init; } = 180;

    /// <summary>Как часто замеряем температуру (мс) — шаг живой шкалы.</summary>
    public int SamplingIntervalMs { get; init; } = 1000;

    /// <summary>Порог авто-стопа по температуре процессора, °C.</summary>
    public int CpuAbortCelsius { get; init; } = 95;

    /// <summary>Порог авто-стопа по температуре видеокарты, °C.</summary>
    public int GpuAbortCelsius { get; init; } = 90;

    /// <summary>С какой температуры процессора считаем «грелся сильно» (для вердикта), °C.</summary>
    public int CpuWarnCelsius { get; init; } = 88;

    /// <summary>С какой температуры видеокарты считаем «грелась сильно» (для вердикта), °C.</summary>
    public int GpuWarnCelsius { get; init; } = 83;

    /// <summary>Сколько потоков нагрузки (null — по числу ядер процессора).</summary>
    public int? ThreadCount { get; init; }

    /// <summary>Запланированная длительность для выбранного вида проверки, сек.</summary>
    public int PlannedSeconds(StressTestKind kind) => kind == StressTestKind.CpuDeep ? DeepSeconds : SafeSeconds;
}
