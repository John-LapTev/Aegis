namespace Aegis.Scanners.Probing;

/// <summary>Снимок состояния драйверов и оборудования (read-only).</summary>
public sealed record DriverSnapshot
{
    /// <summary>Производитель ПК/ноутбука (Win32_ComputerSystem).</summary>
    public required string Manufacturer { get; init; }

    /// <summary>Модель ПК/ноутбука.</summary>
    public required string Model { get; init; }

    /// <summary>Устройства без драйвера или с проблемой (нужно поставить/починить драйвер).</summary>
    public required IReadOnlyList<ProblemDevice> ProblemDevices { get; init; }

    /// <summary>Отключённые устройства (можно включить обратно) — например, случайно отключённый микрофон.</summary>
    public required IReadOnlyList<ProblemDevice> DisabledDevices { get; init; }

    /// <summary>Установленные драйверы (для обзора: что стоит и какой версии).</summary>
    public required IReadOnlyList<DriverInfo> InstalledDrivers { get; init; }

    /// <summary>Видеокарты (для информации и тюнинга).</summary>
    public required IReadOnlyList<GraphicsCard> GraphicsCards { get; init; }
}

/// <summary>Установленный драйвер устройства (read-only) — для списка драйверов.</summary>
public sealed record DriverInfo
{
    /// <summary>Имя устройства.</summary>
    public required string DeviceName { get; init; }

    /// <summary>Категория (видео, сеть, звук…), упрощённая.</summary>
    public required string Category { get; init; }

    /// <summary>Версия драйвера (если известна).</summary>
    public string? Version { get; init; }

    /// <summary>Дата драйвера (если известна).</summary>
    public string? Date { get; init; }

    /// <summary>Идентификатор устройства (PNPDeviceID) — для перезагрузки/переустановки драйвера через pnputil.</summary>
    public string? DeviceId { get; init; }
}

/// <summary>Устройство с проблемой драйвера (read-only).</summary>
public sealed record ProblemDevice
{
    /// <summary>Понятное имя устройства.</summary>
    public required string Name { get; init; }

    /// <summary>Идентификатор устройства (PNPDeviceID) — для поиска драйвера.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Код проблемы Windows (ConfigManagerErrorCode): 28 — драйвер не установлен.</summary>
    public required int ErrorCode { get; init; }

    /// <summary>Драйвер вообще не установлен.</summary>
    public bool NoDriver => ErrorCode == 28;
}

/// <summary>Видеокарта (read-only).</summary>
public sealed record GraphicsCard
{
    /// <summary>Название видеокарты.</summary>
    public required string Name { get; init; }

    /// <summary>Версия установленного драйвера (если известна).</summary>
    public string? DriverVersion { get; init; }
}
