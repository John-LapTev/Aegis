namespace Aegis.Scanners.Probing;

/// <summary>Сведения об обновлении драйвера видеокарты NVIDIA (по их скрытому сервису AjaxDriverService).</summary>
public sealed record DriverUpdate
{
    /// <summary>Последняя доступная версия драйвера (например, «610.62»).</summary>
    public required string LatestVersion { get; init; }

    /// <summary>Прямая ссылка на установщик .exe.</summary>
    public required string DownloadUrl { get; init; }

    /// <summary>Новее ли последняя версия установленной (иначе — уже стоит свежий).</summary>
    public required bool IsNewer { get; init; }

    /// <summary>Установленная версия в формате NVIDIA (например, «576.52»); null — определить не удалось.</summary>
    public string? InstalledVersion { get; init; }
}

