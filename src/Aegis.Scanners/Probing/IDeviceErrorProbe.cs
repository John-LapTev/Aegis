namespace Aegis.Scanners.Probing;

/// <summary>Устройства с ошибками (Диспетчер устройств: код ошибки ≠ 0) — «что-то не работает». Только читает.</summary>
public interface IDeviceErrorProbe
{
    /// <summary>Имена устройств, у которых Windows сообщает о проблеме. Пусто — всё работает.</summary>
    Task<IReadOnlyList<string>> ReadAsync(CancellationToken cancellationToken = default);
}
