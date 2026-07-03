namespace Aegis.Scanners.Probing;

/// <summary>
/// Достоверные показания датчиков железа (через LibreHardwareMonitor): температура ядер процессора и
/// видеокарты, обороты самого быстрого вентилятора, загрузка и частота процессора. Любое поле может быть
/// null — если датчик недоступен или библиотека не запустилась (например, не на Windows).
/// </summary>
public sealed record HardwareReadings
{
    /// <summary>Температура процессора (ядра/пакет), °C.</summary>
    public int? CpuTempCelsius { get; init; }

    /// <summary>Температура видеокарты, °C.</summary>
    public int? GpuTempCelsius { get; init; }

    /// <summary>Обороты самого быстрого вентилятора, об/мин.</summary>
    public int? MaxFanRpm { get; init; }

    /// <summary>Текущая загрузка процессора, %.</summary>
    public int? CpuLoadPercent { get; init; }

    /// <summary>Максимальная текущая частота ядра процессора, МГц (для оценки троттлинга).</summary>
    public int? MaxCpuClockMhz { get; init; }

    /// <summary>Загрузка видеокарты, %.</summary>
    public int? GpuLoadPercent { get; init; }

    /// <summary>Занято видеопамяти, МБ.</summary>
    public int? GpuMemoryUsedMb { get; init; }

    /// <summary>Всего видеопамяти, МБ.</summary>
    public int? GpuMemoryTotalMb { get; init; }

    /// <summary>Потребление процессора (пакет), Вт.</summary>
    public int? CpuPowerWatts { get; init; }

    /// <summary>Потребление видеокарты, Вт.</summary>
    public int? GpuPowerWatts { get; init; }

    /// <summary>Самая высокая температура накопителя (SSD/HDD), °C.</summary>
    public int? StorageMaxTempCelsius { get; init; }

    /// <summary>Есть ли вообще датчик вентилятора (чтобы отличить «стоит» от «нет датчика»).</summary>
    public bool FanPresent { get; init; }

    /// <summary>Название модели процессора (например, «Intel Core i9-12900HX»).</summary>
    public string? CpuName { get; init; }

    /// <summary>Название модели видеокарты (например, «NVIDIA GeForce RTX 3080»).</summary>
    public string? GpuName { get; init; }

    /// <summary>Пустые показания (датчики недоступны).</summary>
    public static readonly HardwareReadings Empty = new();
}
