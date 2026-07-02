namespace Aegis.System.Internal;

/// <summary>
/// Определяет вендора периферии по USB Vendor ID из PnP-идентификатора устройства. Надёжнее, чем имя:
/// игровые мыши/клавиатуры часто показываются как «HID-compliant mouse» без бренда в названии, но VID
/// в железе остаётся. Маппим VID → имя вендора, под который у нас есть фирменная утилита.
/// </summary>
internal static class UsbVendors
{
    // USB VID (4 hex) → понятное имя вендора (совпадает с ключами каталога утилит в UtilitiesScanner).
    private static readonly Dictionary<string, string> ByVid = new(StringComparer.OrdinalIgnoreCase)
    {
        ["046D"] = "Logitech",
        ["1532"] = "Razer",
        ["1038"] = "SteelSeries",
        ["1B1C"] = "Corsair",
        ["0B05"] = "ASUS",
        ["3434"] = "Keychron",
        ["320F"] = "Glorious",
        ["0951"] = "HyperX",
        ["09DA"] = "A4Tech",
    };

    /// <summary>
    /// Имя вендора по PnP-идентификатору (например, <c>USB\VID_046D&amp;PID_C53F\...</c>) или null. Сначала
    /// наш короткий список (точные ключи каталога утилит), затем полная база usb.ids (любой вендор).
    /// </summary>
    public static string? ResolveVendor(string? pnpDeviceId)
    {
        var vid = ExtractCode(pnpDeviceId, "VID_");
        if (vid is null)
        {
            return null;
        }

        return ByVid.GetValueOrDefault(vid) ?? UsbIdDatabase.Instance.VendorName(vid);
    }

    /// <summary>Только модель (без вендора) из usb.ids по VID+PID, или null.</summary>
    public static string? ResolveProductName(string? pnpDeviceId) =>
        UsbIdDatabase.Instance.ProductName(ExtractCode(pnpDeviceId, "VID_"), ExtractCode(pnpDeviceId, "PID_"));

    private static string? ExtractCode(string? pnpDeviceId, string marker)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
        {
            return null;
        }

        var index = pnpDeviceId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 || index + marker.Length + 4 > pnpDeviceId.Length
            ? null
            : pnpDeviceId.Substring(index + marker.Length, 4);
    }
}
