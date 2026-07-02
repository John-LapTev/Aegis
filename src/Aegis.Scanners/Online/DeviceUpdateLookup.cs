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
    // Доверенные домены производителей/официальных каталогов — их предпочитаем как ссылку на загрузку
    // (и эти же сайты добавляем в Google Custom Search, чтобы искать драйверы только по официальным источникам).
    private static readonly string[] TrustedDomains =
    [
        // GPU / чипсеты / процессоры
        "nvidia.com", "amd.com", "intel.com", "evga.com",
        // Игровые рантаймы/библиотеки (DirectX/VC++/.NET — на microsoft.com; Vulkan/OpenGL — Khronos/LunarG)
        "khronos.org", "lunarg.com",
        // Звук / сеть / Wi-Fi / Bluetooth / тачпад
        "realtek.com", "creative.com", "qualcomm.com", "broadcom.com", "mediatek.com",
        "tp-link.com", "killernetworking.com", "synaptics.com",
        // ОС / каталоги драйверов
        "microsoft.com", "apple.com",
        // Материнские платы / ноутбуки / ПК
        "asus.com", "msi.com", "gigabyte.com", "asrock.com", "lenovo.com", "dell.com", "hp.com",
        "acer.com", "samsung.com", "lg.com", "huawei.com", "dynabook.com",
        // Периферия (мышь / клавиатура / гарнитура / стрим)
        "logitech.com", "logi.com", "razer.com", "corsair.com", "steelseries.com", "hyperx.com",
        "elgato.com", "roccat.com", "coolermaster.com", "gloriousgaming.com", "keychron.com",
        "nzxt.com", "thrustmaster.com",
        // Накопители / SSD
        "westerndigital.com", "western-digital.com", "seagate.com", "crucial.com", "kingston.com",
        "sandisk.com", "adata.com",
        // Принтеры / сканеры
        "canon.com", "epson.com", "brother.com",
    ];

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
        var trusted = results.FirstOrDefault(r => IsTrustedDomain(r.Url));
        var version = (trusted is not null ? VersionExtractor.Extract([trusted.Title, trusted.Snippet]) : null)
                      ?? VersionExtractor.Extract(results.Select(r => r.Title).Concat(results.Select(r => r.Snippet)));

        return new DeviceUpdateResult
        {
            LatestVersion = version,
            DownloadUrl = trusted?.Url,
            SourceTitle = trusted?.Title,
        };
    }

    private static bool IsTrustedDomain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return TrustedDomains.Any(d => host == d || host.EndsWith("." + d, StringComparison.Ordinal));
    }
}
