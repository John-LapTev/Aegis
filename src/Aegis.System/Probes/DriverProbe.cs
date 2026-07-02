using System.Globalization;
using System.Management;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник драйверов/оборудования через WMI: модель ПК (Win32_ComputerSystem),
/// устройства с проблемой драйвера (Win32_PnPEntity.ConfigManagerErrorCode), видеокарты
/// (Win32_VideoController). Только читает.
/// </summary>
public sealed class DriverProbe : IDriverProbe
{
    public Task<DriverSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var manufacturer = string.Empty;
        var model = string.Empty;
        var problems = new List<ProblemDevice>();
        var disabled = new List<ProblemDevice>();
        var drivers = new List<DriverInfo>();
        var graphics = new List<GraphicsCard>();

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
            // Нет WMI (не Windows) — модель неизвестна.
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0");
            foreach (var item in searcher.Get())
            {
                using var device = (ManagementObject)item;
                var code = ToInt(device["ConfigManagerErrorCode"]);
                if (code == 0)
                {
                    continue;
                }

                var name = device["Name"]?.ToString();
                var id = device["PNPDeviceID"]?.ToString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                // 45 — устройство сейчас НЕ подключено (съёмное: флешка/док/внешняя карта вынута). Это не
                // поломка драйвера — не пугаем пользователя (DeviceErrorProbe этот код тоже осознанно исключает).
                if (code == 45)
                {
                    continue;
                }

                var entry = new ProblemDevice { Name = EnrichWithPciName(name, id), DeviceId = id, ErrorCode = code };
                // 22 — устройство отключено (можно включить обратно); остальное — проблема драйвера.
                (code == 22 ? disabled : problems).Add(entry);
            }
        }
        catch (Exception)
        {
            // Перечень устройств недоступен — пусто.
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController");
            foreach (var item in searcher.Get())
            {
                using var card = (ManagementObject)item;
                var name = card["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                graphics.Add(new GraphicsCard { Name = name, DriverVersion = card["DriverVersion"]?.ToString() });
            }
        }
        catch (Exception)
        {
            // Видеокарты недоступны — пусто.
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceName, DeviceClass, DriverVersion, DriverDate, DeviceID FROM Win32_PnPSignedDriver");
            foreach (var item in searcher.Get())
            {
                using var driver = (ManagementObject)item;
                var category = CategoryFor(driver["DeviceClass"]?.ToString());
                if (category is null)
                {
                    continue; // показываем только понятные категории (видео/сеть/звук/ввод/Bluetooth/камера)
                }

                var name = driver["DeviceName"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                drivers.Add(new DriverInfo
                {
                    DeviceName = name,
                    Category = category,
                    Version = driver["DriverVersion"]?.ToString(),
                    Date = FormatDriverDate(driver["DriverDate"]?.ToString()),
                    DeviceId = driver["DeviceID"]?.ToString(),
                });

                if (drivers.Count >= 60)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Список драйверов недоступен — пусто.
        }

        return Task.FromResult(new DriverSnapshot
        {
            Manufacturer = manufacturer.Trim(),
            Model = model.Trim(),
            ProblemDevices = problems,
            DisabledDevices = disabled,
            InstalledDrivers = drivers,
            GraphicsCards = graphics,
        });
    }

    private static string? CategoryFor(string? deviceClass) => deviceClass?.ToUpperInvariant() switch
    {
        "DISPLAY" => "Видеокарта",
        "NET" => "Сеть (Wi-Fi/Ethernet)",
        "MEDIA" or "AUDIOENDPOINT" or "SOUND" => "Звук",
        "HIDCLASS" or "MOUSE" or "KEYBOARD" => "Клавиатура/мышь/тачпад",
        "BLUETOOTH" => "Bluetooth",
        "IMAGE" or "CAMERA" => "Камера/сканер",
        "PRINTER" => "Принтер",
        _ => null,
    };

    private static string? FormatDriverDate(string? cimDate)
    {
        // CIM-дата вида 20230115000000.000000-000 → 15.01.2023
        if (string.IsNullOrWhiteSpace(cimDate) || cimDate.Length < 8)
        {
            return null;
        }

        var year = cimDate[..4];
        var month = cimDate.Substring(4, 2);
        var day = cimDate.Substring(6, 2);
        return $"{day}.{month}.{year}";
    }

    private static int ToInt(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>
    /// Дополнить имя устройства моделью из pci.ids по VEN/DEV в PNPDeviceID (PCI\VEN_10DE&amp;DEV_2484&amp;…).
    /// Полезно для устройств БЕЗ драйвера, которые Windows показывает обезличенно («Базовое системное устройство»):
    /// человек видит, что это, например, сетевая карта Realtek. Если модель уже есть в имени — не дублируем.
    /// </summary>
    private static string EnrichWithPciName(string name, string deviceId)
    {
        var vendorId = ExtractPciHex(deviceId, "VEN_");
        if (vendorId is null)
        {
            return name; // не PCI-устройство (USB/HID и т.п.) — оставляем как есть.
        }

        var database = PciIdDatabase.Instance;
        var deviceName = database.DeviceName(vendorId, ExtractPciHex(deviceId, "DEV_"));
        var vendorName = database.VendorName(vendorId);

        var resolved = vendorName is not null && deviceName is not null
            ? $"{vendorName} {deviceName}"
            : deviceName ?? vendorName;

        if (resolved is null || name.Contains(resolved, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return $"{name} — {resolved}";
    }

    /// <summary>Достать 4-значный hex после токена (VEN_/DEV_) из PNPDeviceID, в нижнем регистре; null если нет.</summary>
    private static string? ExtractPciHex(string deviceId, string token)
    {
        var index = deviceId.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || index + token.Length + 4 > deviceId.Length)
        {
            return null;
        }

        var hex = deviceId.Substring(index + token.Length, 4);
        return hex.All(Uri.IsHexDigit) ? hex.ToLowerInvariant() : null;
    }
}
