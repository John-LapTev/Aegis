using Aegis.Core.Models;

namespace Aegis.Scanners.Drivers;

/// <summary>
/// Сопоставляет установленное устройство с доступным обновлением драйвера из каталога Windows Update.
/// Точное сопоставление — по аппаратному идентификатору (PNPDeviceID установленного содержит HardwareID
/// предложения); запасное — по имени устройства. Чистая функция без ввода-вывода → покрыта тестами на любой ОС.
/// </summary>
public static class DriverUpdateMatcher
{
    /// <summary>Найти обновление для устройства (по его PNPDeviceID и имени). null — обновления нет.</summary>
    public static DriverUpdateOffer? Match(
        IReadOnlyList<DriverUpdateOffer> offers, string? deviceId, string? deviceName)
    {
        if (offers.Count == 0)
        {
            return null;
        }

        // 1. Точно — по аппаратному ID: экземплярный путь устройства (PCI\VEN_10DE&DEV_2482&SUBSYS…\4&…)
        //    начинается с/содержит аппаратный ID предложения (PCI\VEN_10DE&DEV_2482).
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var byHardware = offers.FirstOrDefault(o =>
                !string.IsNullOrWhiteSpace(o.HardwareId)
                && deviceId.Contains(o.HardwareId!, StringComparison.OrdinalIgnoreCase));
            if (byHardware is not null)
            {
                return byHardware;
            }
        }

        // 2. Запасное — по имени устройства (в обе стороны, чтобы «Realtek Audio» ↔ «Realtek High Definition Audio»).
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            return offers.FirstOrDefault(o =>
                !string.IsNullOrWhiteSpace(o.DeviceName)
                && NamesOverlap(deviceName, o.DeviceName!));
        }

        return null;
    }

    private static bool NamesOverlap(string a, string b)
    {
        var x = a.Trim();
        var y = b.Trim();
        return x.Contains(y, StringComparison.OrdinalIgnoreCase)
               || y.Contains(x, StringComparison.OrdinalIgnoreCase);
    }
}
