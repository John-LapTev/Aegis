namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик состояния драйверов/оборудования (модель ПК, устройства без драйвера, видеокарты).
/// Только ЧИТАЕТ (WMI). Логика и тексты — в <see cref="Drivers.DriversScanner"/>.
/// </summary>
public interface IDriverProbe
{
    Task<DriverSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
