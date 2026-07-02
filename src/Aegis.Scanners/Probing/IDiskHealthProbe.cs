namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик здоровья дисков по SMART. Только ЧИТАЕТ. Windows-реализация (WMI
/// <c>MSStorageDriver_FailurePredictStatus</c> / SMART-атрибуты) — в слое доступа к системе;
/// логика и тексты — в <see cref="SystemInfo.DiskHealthScanner"/>.
/// </summary>
public interface IDiskHealthProbe
{
    /// <summary>Считать здоровье всех дисков.</summary>
    Task<IReadOnlyList<SmartDriveHealth>> ReadAsync(CancellationToken cancellationToken = default);
}
