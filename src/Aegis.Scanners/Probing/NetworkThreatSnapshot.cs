namespace Aegis.Scanners.Probing;

/// <summary>Снимок сетевого состояния для поиска угроз (read-only).</summary>
public sealed record NetworkThreatSnapshot
{
    /// <summary>Записи файла hosts (домен → адрес).</summary>
    public required IReadOnlyList<HostsEntry> HostsEntries { get; init; }

    /// <summary>Настроенные DNS-серверы.</summary>
    public required IReadOnlyList<string> DnsServers { get; init; }

    /// <summary>Подозрительные сетевые подключения процессов (пробник уже определил причину).</summary>
    public required IReadOnlyList<SuspiciousConnection> SuspiciousConnections { get; init; }

    /// <summary>Все активные подключения — сканер сам классифицирует их по портам (майнинг-пулы, Tor).</summary>
    public required IReadOnlyList<ActiveConnection> ActiveConnections { get; init; }
}
