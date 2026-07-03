using System;
using System.Collections.Generic;
using System.Linq;

namespace Aegis.Core;

/// <summary>
/// Единый белый список официальных доменов производителей/каталогов. Используется и для выбора ссылки на драйвер
/// (DeviceUpdateLookup), и для проверки ссылок из СВОБОДНОГО ответа ИИ — чтобы не выдать галлюцинированный/фейковый
/// сайт за «официальную страницу» доверчивому пользователю (аудит 2026-07-03).
/// </summary>
public static class TrustedDomains
{
    public static readonly IReadOnlyList<string> Domains =
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

    /// <summary>Ведёт ли URL на доверенный домен (сам домен или его поддомен, напр. downloads.nvidia.com).</summary>
    public static bool IsTrusted(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return Domains.Any(d => host == d || host.EndsWith("." + d, StringComparison.Ordinal));
    }
}
