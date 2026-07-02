namespace Aegis.Scanners.Probing;

/// <summary>
/// Один обратимый переключатель приватности (реестровый). Пробник заполняет состояние и координаты,
/// сканер показывает включённые, фабрика правок строит из координат обратимое выключение.
/// </summary>
public sealed record PrivacyToggle
{
    /// <summary>Стабильный Id находки (например, <c>privacy-cortana</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Заголовок простыми словами.</summary>
    public required string Title { get; init; }

    /// <summary>Короткая деталь.</summary>
    public required string Detail { get; init; }

    /// <summary>Объяснение «?» простыми словами.</summary>
    public required string Explain { get; init; }

    /// <summary>Включено ли сейчас (только включённое имеет смысл предлагать выключить).</summary>
    public required bool Enabled { get; init; }

    /// <summary>Куст реестра ("HKLM"/"HKCU").</summary>
    public required string Hive { get; init; }

    /// <summary>Подключ реестра.</summary>
    public required string SubKey { get; init; }

    /// <summary>Имя значения.</summary>
    public required string ValueName { get; init; }

    /// <summary>Значение, которое выставляем при выключении (обычно 0 или 1).</summary>
    public required int DisableValue { get; init; }
}
