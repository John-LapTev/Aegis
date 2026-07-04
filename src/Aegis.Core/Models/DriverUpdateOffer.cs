namespace Aegis.Core.Models;

/// <summary>
/// Доступное обновление драйвера из официального каталога Windows Update (не только NVIDIA — любое устройство).
/// Само наличие предложения означает, что Windows считает драйвер более свежим/применимым, чем установленный.
/// Best-effort: точную числовую версию WUA отдаёт не всегда, поэтому опираемся на дату/заголовок.
/// </summary>
public sealed record DriverUpdateOffer
{
    /// <summary>Человеко-понятный заголовок обновления (как в Windows Update).</summary>
    public required string Title { get; init; }

    /// <summary>Имя устройства/модели драйвера (DriverModel) — для сопоставления с установленным.</summary>
    public string? DeviceName { get; init; }

    /// <summary>Аппаратный идентификатор (DriverHardwareID, например <c>PCI\VEN_10DE&amp;DEV_2482</c>) — точное сопоставление.</summary>
    public string? HardwareId { get; init; }

    /// <summary>Производитель драйвера (DriverProvider): Intel, Realtek, AMD…</summary>
    public string? Provider { get; init; }

    /// <summary>Дата доступного драйвера (DriverVerDate, формат yyyy-MM-dd) — для сравнения с установленным.</summary>
    public string? Date { get; init; }
}
