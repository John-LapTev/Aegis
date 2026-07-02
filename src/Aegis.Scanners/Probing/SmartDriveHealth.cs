namespace Aegis.Scanners.Probing;

/// <summary>
/// Нормализованное здоровье одного диска по SMART (read-only). Сырые SMART-атрибуты
/// интерпретирует Windows-пробник и выдаёт готовый <see cref="Level"/> + ключевые метрики.
/// </summary>
public sealed record SmartDriveHealth
{
    /// <summary>Имя/буква диска (например, «C:») — служит идентификатором.</summary>
    public required string Name { get; init; }

    /// <summary>Итоговый уровень здоровья (зона шкалы).</summary>
    public required SmartHealthLevel Level { get; init; }

    /// <summary>Модель накопителя (если известна).</summary>
    public string? Model { get; init; }

    /// <summary>Износ SSD в процентах (0..100), если применимо.</summary>
    public int? PercentLifeUsed { get; init; }

    /// <summary>Число переназначенных секторов (HDD) — рост говорит о деградации.</summary>
    public int? ReallocatedSectorCount { get; init; }

    /// <summary>Температура диска в °C (если известна).</summary>
    public int? TemperatureCelsius { get; init; }

    /// <summary>Насколько диск заполнен, % (0..100) — сумма по его томам/буквам; null, если не удалось сопоставить.</summary>
    public int? FillPercent { get; init; }

    /// <summary>На диске есть раздел, но Windows не читает его формат (RAW / отформатирован не под Windows) — заполнение взять негде.</summary>
    public bool FilesystemUnreadable { get; init; }

    /// <summary>Буква диска (первого тома), например 'C' — для подписи на иконке; '\0'/null, если буквы нет (RAW/без буквы).</summary>
    public char? Letter { get; init; }
}
