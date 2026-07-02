namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик сетевого состояния (hosts, DNS, подозрительные подключения). Только ЧИТАЕТ.
/// Windows-реализация — в слое доступа к системе; классификация и тексты — в
/// <see cref="Threats.NetworkThreatScanner"/>.
/// </summary>
public interface INetworkThreatProbe
{
    /// <summary>Считать снимок сетевого состояния.</summary>
    Task<NetworkThreatSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
