namespace Aegis.Scanners.Probing;

/// <summary>Снимок для раздела «Утилиты»: производитель/модель ПК, установленные программы, вендоры периферии.</summary>
public sealed record UtilitiesSnapshot
{
    /// <summary>Производитель ПК/ноутбука (Lenovo, ASUS, MSI, Dell, HP…).</summary>
    public required string Manufacturer { get; init; }

    /// <summary>Модель ПК/ноутбука.</summary>
    public required string Model { get; init; }

    /// <summary>Названия установленных программ (из реестра удаления) — чтобы понять, что уже стоит.</summary>
    public required IReadOnlyList<string> InstalledPrograms { get; init; }

    /// <summary>Вендоры подключённой периферии (мышь/клавиатура): Logitech, Razer, SteelSeries…</summary>
    public required IReadOnlyList<string> PeripheralVendors { get; init; }

    /// <summary>Понятные имена опознанных подключённых устройств (по базе usb.ids) — чтобы показать пользователю.</summary>
    public IReadOnlyList<string> DetectedDevices { get; init; } = [];

    /// <summary>Есть ли интернет (для предупреждения «нет интернета, чтобы найти утилиты»).</summary>
    public required bool HasInternet { get; init; }
}
