using Aegis.Scanners.Internal;

namespace Aegis.Scanners.Probing;

/// <summary>
/// Чтение хранилища драйверов Windows: какие пакеты там лежат и какие из них сейчас используются железом.
/// Только читает. Реализация Windows-специфична.
/// </summary>
public interface IDriverStoreProbe
{
    /// <summary>Все пакеты драйверов из хранилища.</summary>
    Task<IReadOnlyList<DriverPackage>> ReadPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Имена пакетов (oemNN.inf), которыми сейчас пользуются устройства — их трогать нельзя.</summary>
    Task<IReadOnlySet<string>> ReadActivePackagesAsync(CancellationToken cancellationToken = default);
}
