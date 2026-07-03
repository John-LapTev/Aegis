namespace Aegis.Scanners.Probing;

/// <summary>
/// Быстрые показатели «здоровья» компьютера прямо сейчас: оперативная память, время без перезагрузки,
/// текущая загрузка процессора и обороты вентиляторов. Часть может быть недоступна (железо не отдаёт) —
/// такие поля приходят как null/0, и в UI показываем честное «датчик недоступен».
/// </summary>
public sealed record SystemVitals
{
    /// <summary>Всего оперативной памяти, байт (0 — не удалось прочитать).</summary>
    public long RamTotalBytes { get; init; }

    /// <summary>Свободно оперативной памяти прямо сейчас, байт.</summary>
    public long RamAvailableBytes { get; init; }

    /// <summary>Время с последней загрузки Windows, секунд.</summary>
    public long UptimeSeconds { get; init; }

    /// <summary>Текущая загрузка процессора, % (null — не удалось измерить).</summary>
    public int? CpuLoadPercent { get; init; }

    /// <summary>Обороты самого быстрого вентилятора, об/мин (0 — есть, но стоит; null — датчика нет).</summary>
    public int? FanRpm { get; init; }

    /// <summary>Есть ли вообще датчик вентилятора (чтобы отличить «стоит» от «нет датчика»).</summary>
    public bool FanPresent { get; init; }

    /// <summary>Загрузка видеокарты, % (null — недоступно).</summary>
    public int? GpuLoadPercent { get; init; }

    /// <summary>Занято видеопамяти, МБ.</summary>
    public int? GpuMemoryUsedMb { get; init; }

    /// <summary>Всего видеопамяти, МБ.</summary>
    public int? GpuMemoryTotalMb { get; init; }

    /// <summary>Потребление процессора, Вт.</summary>
    public int? CpuPowerWatts { get; init; }

    /// <summary>Потребление видеокарты, Вт.</summary>
    public int? GpuPowerWatts { get; init; }

    /// <summary>Текущая частота процессора, МГц.</summary>
    public int? CpuClockMhz { get; init; }

    /// <summary>Модель процессора (например, «Intel Core i9-12900HX»), если удалось прочитать.</summary>
    public string? CpuName { get; init; }

    /// <summary>Модель видеокарты (например, «NVIDIA GeForce RTX 3080»), если удалось прочитать.</summary>
    public string? GpuName { get; init; }

    /// <summary>Занято оперативной памяти, байт.</summary>
    public long RamUsedBytes => Math.Max(0, RamTotalBytes - RamAvailableBytes);

    /// <summary>Занято оперативной памяти, % (0, если объём неизвестен).</summary>
    public int RamUsedPercent => RamTotalBytes > 0 ? (int)Math.Round(100.0 * RamUsedBytes / RamTotalBytes) : 0;
}
