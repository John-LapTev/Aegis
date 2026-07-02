using System.Management;
using System.Net.NetworkInformation;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник для раздела «Утилиты»: производитель/модель (Win32_ComputerSystem), установленные
/// программы (реестр удаления), периферия (Win32_PnPEntity, HID) и наличие сети. Только читает.
/// </summary>
public sealed class UtilitiesProbe : IUtilitiesProbe
{
    public Task<UtilitiesSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var manufacturer = string.Empty;
        var model = string.Empty;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            foreach (var item in searcher.Get())
            {
                using var system = (ManagementObject)item;
                manufacturer = system["Manufacturer"]?.ToString() ?? string.Empty;
                model = system["Model"]?.ToString() ?? string.Empty;
                break;
            }
        }
        catch (Exception)
        {
            // Нет WMI (не Windows) — производитель неизвестен.
        }

        var peripherals = new List<string>();
        var detected = new List<string>();
        try
        {
            // Шире обзор: HID/мышь/клавиатура + USB (донглы) + Bluetooth + камеры/сканеры (Image) + принтеры.
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, PNPClass, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass='HIDClass' OR " +
                "PNPClass='Mouse' OR PNPClass='Keyboard' OR PNPClass='USB' OR PNPClass='Bluetooth' OR " +
                "PNPClass='Image' OR PNPClass='Camera' OR PNPClass='Printer'");
            foreach (var item in searcher.Get())
            {
                using var device = (ManagementObject)item;
                var pnpId = device["PNPDeviceID"]?.ToString();

                var combined = $"{device["Manufacturer"]} {device["Name"]}".Trim();
                if (combined.Length > 0)
                {
                    peripherals.Add(combined);
                }

                // Вендор по USB Vendor ID (наш список + полная база usb.ids) — для подбора фирменной утилиты.
                var byVid = Internal.UsbVendors.ResolveVendor(pnpId);
                if (byVid is not null)
                {
                    peripherals.Add(byVid);
                }

                // Список устройств — ПО-ЧЕЛОВЕЧЕСКИ: тип (Мышь/Клавиатура/Камера/Bluetooth) + модель, если
                // известна по usb.ids. Названия чипов-производителей пользователю не нужны.
                var deviceClass = device["PNPClass"]?.ToString();
                if (deviceClass is not null && DeviceTypeByClass.TryGetValue(deviceClass, out var typeRu))
                {
                    // Модель из usb.ids; если не нашли — честно «(модель не определена)», чтобы было понятно,
                    // почему под это устройство не подобралась фирменная утилита.
                    var deviceModel = Internal.UsbVendors.ResolveProductName(pnpId);
                    var label = deviceModel is not null ? $"{typeRu} — {deviceModel}" : $"{typeRu} (модель не определена)";
                    if (!detected.Contains(label))
                    {
                        detected.Add(label);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Нет WMI — периферия неизвестна.
        }

        var hasInternet = false;
        try
        {
            hasInternet = NetworkInterface.GetIsNetworkAvailable();
        }
        catch (Exception)
        {
            // Не удалось определить сеть — считаем, что её нет (покажем предупреждение).
        }

        return Task.FromResult(new UtilitiesSnapshot
        {
            Manufacturer = manufacturer,
            Model = model,
            InstalledPrograms = Internal.InstalledPrograms.Read(),
            PeripheralVendors = peripherals,
            DetectedDevices = detected.Take(25).ToList(),
            HasInternet = hasInternet,
        });
    }

    // PNPClass устройства → понятный тип по-русски (для списка «Подключённые устройства»).
    private static readonly Dictionary<string, string> DeviceTypeByClass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mouse"] = "Мышь",
        ["Keyboard"] = "Клавиатура",
        ["Bluetooth"] = "Bluetooth-устройство",
        ["Image"] = "Камера / сканер",
        ["Camera"] = "Камера",
        ["Printer"] = "Принтер",
    };
}
