namespace Aegis.Core.Models;

/// <summary>
/// Точка истории здоровья дисков — снимок ключевых SMART-показателей на момент проверки.
/// Копится из проверки в проверку, чтобы Aegis видел ДИНАМИКУ и заранее предупреждал
/// («диск начал сыпаться», «износ растёт быстро», «греется сильнее прежнего»).
/// </summary>
public sealed record HealthTrendSnapshot
{
    /// <summary>Когда снят снимок.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Показатели каждого диска в этот момент.</summary>
    public IReadOnlyList<DiskTrendPoint> Disks { get; init; } = [];
}

/// <summary>Показатели одного диска в точке истории (только то, что меняется со временем и важно для тренда).</summary>
public sealed record DiskTrendPoint
{
    /// <summary>Имя диска — по нему сопоставляем один и тот же диск между проверками.</summary>
    public required string Name { get; init; }

    /// <summary>Износ SSD в процентах (сколько ресурса записи израсходовано), если известно.</summary>
    public int? PercentLifeUsed { get; init; }

    /// <summary>Число переназначенных секторов — главный признак «диск сыплется», если известно.</summary>
    public int? ReallocatedSectorCount { get; init; }

    /// <summary>Температура диска в °C, если известно.</summary>
    public int? TemperatureCelsius { get; init; }

    /// <summary>Заполнение диска в процентах, если известно.</summary>
    public int? FillPercent { get; init; }
}
