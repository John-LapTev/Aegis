namespace Aegis.Core.Models;

/// <summary>Сведения о доступном обновлении (последний релиз на GitHub новее текущей версии).</summary>
public sealed record UpdateInfo
{
    /// <summary>Версия релиза без «v» («2.78.0»).</summary>
    public required string Version { get; init; }

    /// <summary>Прямая ссылка на .exe нового релиза (asset browser_download_url).</summary>
    public required string DownloadUrl { get; init; }

    /// <summary>Ожидаемый размер .exe в байтах (asset size из GitHub API) — для проверки целостности закачки. 0 — неизвестен.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Описание релиза (что нового) — для показа пользователю; может быть пустым.</summary>
    public string? Notes { get; init; }
}
