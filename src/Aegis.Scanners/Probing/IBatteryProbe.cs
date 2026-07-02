namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик данных о батарее (заводская/текущая ёмкость → износ). Только читает. Вердикт простыми
/// словами — в <see cref="Programs.BatteryScanner"/>.
/// </summary>
public interface IBatteryProbe
{
    Task<BatterySnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
