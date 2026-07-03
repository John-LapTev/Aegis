using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Scanners.Online;

/// <summary>
/// Ищет драйвер/утилиту для устройства в интернете (через <see cref="IWebSearch"/>): официальная ссылка на
/// загрузку + приблизительная последняя версия из выдачи. Предпочитает результаты с официальных доменов вендоров,
/// чтобы не вести на сомнительные «сборники драйверов». Best-effort: нет сети/результатов → пустой результат.
/// </summary>
public sealed class DeviceUpdateLookup : IDeviceUpdateLookup
{
    private readonly IWebSearch _search;

    public DeviceUpdateLookup(IWebSearch search) => _search = search;

    public async Task<DeviceUpdateResult> LookupAsync(
        string deviceName, DeviceLookupKind kind = DeviceLookupKind.Driver, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return DeviceUpdateResult.Empty;
        }

        var query = kind == DeviceLookupKind.Utility
            ? $"{deviceName} software utility download"
            : $"{deviceName} driver latest version download";

        var results = await _search.SearchAsync(query, 6, cancellationToken).ConfigureAwait(false);
        if (results.Count == 0)
        {
            return DeviceUpdateResult.Empty;
        }

        // Ссылку «Открыть страницу» даём ТОЛЬКО на доверенный домен (официальный производитель/крупный источник).
        // Если доверенного в выдаче нет — ссылку НЕ выдаём (не отправляем человека на сомнительный сайт-сборник
        // драйверов), но версию всё равно можем показать из выдачи — это информация, а не кнопка перехода (правка аудита).
        var trusted = results.FirstOrDefault(r => TrustedDomains.IsTrusted(r.Url));
        var version = (trusted is not null ? VersionExtractor.Extract([trusted.Title, trusted.Snippet]) : null)
                      ?? VersionExtractor.Extract(results.Select(r => r.Title).Concat(results.Select(r => r.Snippet)));

        return new DeviceUpdateResult
        {
            LatestVersion = version,
            DownloadUrl = trusted?.Url,
            SourceTitle = trusted?.Title,
        };
    }
}
