namespace Aegis.Core.Models;

/// <summary>
/// Результат поиска обновления для устройства/утилиты в интернете: официальная ссылка на загрузку и
/// приблизительная последняя версия из выдачи (точность не гарантируется — пометить как «≈»/«проверь по ссылке»).
/// </summary>
public sealed record DeviceUpdateResult
{
    /// <summary>Последняя версия из выдачи (приблизительно) или null, если не удалось извлечь.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Официальная ссылка на загрузку драйвера/утилиты (первый подходящий результат) или null.</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Заголовок найденного результата (для пояснения, откуда данные).</summary>
    public string? SourceTitle { get; init; }

    /// <summary>Нашлось ли что-то полезное (ссылка или версия).</summary>
    public bool Found => DownloadUrl is not null || LatestVersion is not null;

    public static DeviceUpdateResult Empty { get; } = new();
}
