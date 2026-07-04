using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Utilities;

/// <summary>
/// Раздел «Утилиты» (группа <see cref="ScanGroup.Missing"/>): по производителю/модели ПК и подключённой
/// периферии подсказывает нужные фирменные утилиты (Lenovo Vantage, Armoury Crate, MSI Center, G HUB…),
/// показывает, установлены ли они. Установка — через встроенный установщик Windows (winget): он сам качает
/// и ставит молча, неверный пакет просто не найдётся (ничего лишнего не поставит). Где winget недоступен —
/// кнопка открывает официальную страницу. Без интернета — предупреждение.
/// </summary>
public sealed class UtilitiesScanner : IScanner
{
    private const string PcSection = "Для твоего компьютера";
    private const string DeviceSection = "Для подключённых устройств";
    private const string ConnectedSection = "Подключённые устройства";

    /// <summary>Каталог фирменных утилит ПК: (ключ производителя, имя, офиц. страница, маркеры «установлено», winget-аргументы).</summary>
    private static readonly Utility[] PcUtilities =
    [
        new("Lenovo", "Lenovo Vantage", "https://apps.microsoft.com/detail/9wzdncrfj4mv", ["Lenovo Vantage", "Vantage"], "--id 9WZDNCRFJ4MV --source msstore"),
        new("ASUS", "Armoury Crate", "https://www.asus.com/supportonly/armoury%20crate/helpdesk_download/", ["Armoury Crate"], null),
        new("Micro-Star", "MSI Center", "https://www.msi.com/Landing/MSI-Center", ["MSI Center"], null),
        new("MSI", "MSI Center", "https://www.msi.com/Landing/MSI-Center", ["MSI Center"], null),
        new("Dell", "Dell Command Update", "https://www.dell.com/support/kbdoc/000177325", ["SupportAssist", "Dell Update", "Dell Command"], "--id Dell.CommandUpdate"),
        new("Hewlett", "HP Support Assistant", "https://support.hp.com/us-en/help/hp-support-assistant", ["HP Support Assistant"], null),
        new("HP", "HP Support Assistant", "https://support.hp.com/us-en/help/hp-support-assistant", ["HP Support Assistant"], null),
        new("Acer", "Acer Care Center", "https://www.acer.com/us-en/support", ["Care Center"], null),
        new("Gigabyte", "Gigabyte Control Center", "https://www.gigabyte.com/Support/Utility", ["Control Center", "GIGABYTE"], null),
    ];

    /// <summary>Каталог утилит периферии: (ключ вендора, имя, офиц. страница, маркеры «установлено», winget-аргументы).</summary>
    private static readonly Utility[] DeviceUtilities =
    [
        new("Logitech", "Logitech G HUB", "https://www.logitechg.com/innovation/g-hub.html", ["G HUB", "Logi Options", "Logitech"], "--id Logitech.GHUB"),
        new("Razer", "Razer Synapse", "https://www.razer.com/synapse-3", ["Synapse"], "--id Razer.Synapse3"),
        new("SteelSeries", "SteelSeries GG", "https://steelseries.com/gg", ["SteelSeries GG", "SteelSeries Engine"], "--id SteelSeries.GG"),
        new("Corsair", "Corsair iCUE", "https://www.corsair.com/icue", ["iCUE"], "--id Corsair.iCUE.4"),
        new("HyperX", "HyperX NGENUITY", "https://hyperx.com/pages/ngenuity", ["NGENUITY", "HyperX"], "--id HP.HyperXNGENUITY"),
        new("Glorious", "Glorious CORE", "https://www.gloriousgaming.com/pages/software", ["Glorious CORE", "Glorious"], null),
        new("Keychron", "Keychron Launcher", "https://launcher.keychron.com", ["Keychron"], null),
        new("A4Tech", "A4Tech / Bloody", "https://www.bloody.com/en/download.php", ["Bloody", "A4Tech"], null),
    ];

    private readonly IUtilitiesProbe _probe;

    public UtilitiesScanner(IUtilitiesProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Missing;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        if (!snapshot.HasInternet)
        {
            findings.Add(new Finding
            {
                Id = "util-no-internet",
                Group = ScanGroup.Missing,
                Severity = Severity.Warning,
                Title = "Нет подключения к интернету",
                Detail = "не получится найти и скачать утилиты",
                Explain = "Чтобы найти и скачать фирменные утилиты для твоего компьютера, нужен интернет. " +
                          "Подключись к сети и запусти проверку этого раздела ещё раз.",
                Data = new Dictionary<string, string> { [FindingDataKeys.Section] = PcSection },
            });
        }

        foreach (var utility in PcUtilities)
        {
            // Dedup, как в цикле устройств ниже: две записи каталога дают один Id (напр. «Micro-Star»+«MSI»→MSI Center),
            // и строка производителя с обоими подстроками не должна давать дубль-находку/коллизию whitelist (аудит).
            if (findings.Any(f => f.Id == UtilityId("util-pc", utility.Name)))
            {
                continue;
            }

            if (Contains(snapshot.Manufacturer, utility.Key))
            {
                findings.Add(CreateUtilityFinding(utility, snapshot.InstalledPrograms, PcSection, "util-pc"));
            }
        }

        // Утилиты периферии показываем, если вендор устройства определён ЛИБО утилита уже установлена
        // (тогда покажем со статусом «установлено») — чтобы установленные точно были видны, даже если
        // конкретную мышь/клавиатуру не удалось распознать по «железу».
        foreach (var utility in DeviceUtilities)
        {
            if (findings.Any(f => f.Id == UtilityId("util-dev", utility.Name)))
            {
                continue;
            }

            var vendorDetected = snapshot.PeripheralVendors.Any(v => Contains(v, utility.Key));
            var isInstalled = utility.Markers.Any(m => snapshot.InstalledPrograms.Any(p => Contains(p, m)));
            if (vendorDetected || isInstalled)
            {
                findings.Add(CreateUtilityFinding(utility, snapshot.InstalledPrograms, DeviceSection, "util-dev"));
            }
        }

        // Показать опознанные подключённые устройства (по базе usb.ids) — чтобы человек видел свою периферию.
        foreach (var device in snapshot.DetectedDevices)
        {
            findings.Add(new Finding
            {
                Id = "util-device-" + Sanitize(device),
                Group = ScanGroup.Missing,
                Severity = Severity.Ok,
                Title = device,
                Detail = "подключённое устройство",
                Explain = "Это одно из подключённых к компьютеру устройств — опознано по USB-коду. Просто список, " +
                          "ничего делать не нужно. Если для устройства есть фирменная утилита, она показана выше.",
                Data = new Dictionary<string, string> { [FindingDataKeys.Section] = ConnectedSection },
            });
        }

        if (findings.All(f => f.Id == "util-no-internet"))
        {
            findings.Add(new Finding
            {
                Id = "util-none",
                Group = ScanGroup.Missing,
                Severity = Severity.Ok,
                Title = "Подходящих фирменных утилит не найдено",
                Detail = "ничего дополнительно ставить не нужно",
                Explain = "Для твоего компьютера и устройств не нашлось известных фирменных утилит, которые стоило бы " +
                          "доустановить. Это нормально — значит, всё нужное уже есть или не требуется.",
                Data = new Dictionary<string, string> { [FindingDataKeys.Section] = PcSection },
            });
        }

        return new ScanResult { Group = ScanGroup.Missing, Findings = findings };
    }

    private static Finding CreateUtilityFinding(
        Utility utility,
        IReadOnlyList<string> installed,
        string section,
        string idPrefix)
    {
        var isInstalled = utility.Markers.Any(m => installed.Any(p => Contains(p, m)));
        var data = new Dictionary<string, string> { [FindingDataKeys.Section] = section, ["url"] = utility.Url };

        // Кнопка установки/переустановки через winget — и для НЕустановленных («Установить»), и для уже
        // установленных («Переустановить» — winget сам перекачает и поставит молча, чинит/обновляет).
        if (!string.IsNullOrEmpty(utility.WingetArgs))
        {
            data[FindingDataKeys.Kind] = FindingKinds.WingetInstall;
            data["winget"] = utility.WingetArgs;
            if (isInstalled)
            {
                data["reinstall"] = "1";
            }
        }

        return new Finding
        {
            Id = UtilityId(idPrefix, utility.Name),
            Group = ScanGroup.Missing,
            Severity = isInstalled ? Severity.Ok : Severity.Info,
            Title = utility.Name,
            Detail = isInstalled ? "уже установлено" : "рекомендуется установить",
            Explain = isInstalled
                ? $"Фирменная утилита «{utility.Name}» уже установлена — это хорошо. Через неё обновляются драйверы " +
                  "и настраиваются функции твоего устройства. Если работает со сбоями — кнопка «Переустановить» " +
                  "перекачает и поставит её заново тихо (через встроенный winget)."
                : $"«{utility.Name}» — официальная программа для твоего устройства: через неё удобно обновлять драйверы " +
                  "и управлять функциями (режимы мощности, подсветка, кнопки и т.п.). Кнопка «Установить» скачает и " +
                  "поставит её тихо через встроенный установщик Windows; «Открыть страницу» — официальный сайт.",
            Data = data,
        };
    }

    private static string UtilityId(string prefix, string name) => $"{prefix}-{Sanitize(name)}";

    private static bool Contains(string haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string Sanitize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Take(40).ToArray());

    private sealed record Utility(string Key, string Name, string Url, string[] Markers, string? WingetArgs);
}
