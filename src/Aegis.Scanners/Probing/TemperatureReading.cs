namespace Aegis.Scanners.Probing;

/// <summary>Показание температуры компонента для шкалы 🟢/🟡/🔴 в группе «Система».</summary>
public sealed record TemperatureReading
{
    /// <summary>Что измерено: «Процессор» / «Видеокарта».</summary>
    public required string Component { get; init; }

    /// <summary>Температура в °C (null — датчик недоступен).</summary>
    public int? Celsius { get; init; }

    /// <summary>Модель железа (например, «Intel Core i9-12900HX»), если удалось определить.</summary>
    public string? Model { get; init; }
}
