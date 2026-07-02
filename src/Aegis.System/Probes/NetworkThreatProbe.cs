using System.Diagnostics;
using System.Net.NetworkInformation;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник сетевого состояния: файл hosts, DNS-серверы, активные TCP-подключения (с именем
/// программы-владельца через <c>netstat -ano</c>). Только читает. Классификацию (майнинг-пулы, Tor,
/// подмена hosts) делает <see cref="Aegis.Scanners.Threats.NetworkThreatScanner"/>.
/// </summary>
public sealed class NetworkThreatProbe : INetworkThreatProbe
{
    public async Task<NetworkThreatSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var tcpOwners = await ReadTcpOwnersAsync(cancellationToken).ConfigureAwait(false);

        return new NetworkThreatSnapshot
        {
            HostsEntries = ReadHosts(),
            DnsServers = ReadDns(),
            SuspiciousConnections = [],
            ActiveConnections = ReadActiveConnections(tcpOwners),
        };
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadTcpOwnersAsync(CancellationToken cancellationToken)
    {
        var output = await ProcessRunner
            .RunForOutputAsync(ProcessRunner.System("netstat.exe"), "-ano", cancellationToken)
            .ConfigureAwait(false);
        return NetstatParser.ParseTcpPidMap(output);
    }

    private static IReadOnlyList<HostsEntry> ReadHosts()
    {
        var entries = new List<HostsEntry>();
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            if (!File.Exists(path))
            {
                return entries;
            }

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    entries.Add(new HostsEntry { MappedIp = parts[0], Hostname = parts[1] });
                }
            }
        }
        catch (Exception)
        {
            // Файл hosts недоступен — пусто.
        }

        return entries;
    }

    private static IReadOnlyList<string> ReadDns()
    {
        var servers = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                foreach (var address in nic.GetIPProperties().DnsAddresses)
                {
                    servers.Add(address.ToString());
                }
            }
        }
        catch (Exception)
        {
            // Сетевые интерфейсы недоступны — пусто.
        }

        return servers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<ActiveConnection> ReadActiveConnections(IReadOnlyDictionary<string, int> tcpOwners)
    {
        var connections = new List<ActiveConnection>();
        var nameByPid = new Dictionary<int, string>();
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            foreach (var connection in properties.GetActiveTcpConnections())
            {
                if (connection.State != TcpState.Established)
                {
                    continue;
                }

                var address = connection.RemoteEndPoint.Address.ToString();
                var port = connection.RemoteEndPoint.Port;
                // PID ищем по ЛОКАЛЬНОЙ точке (уникальна), а показываем удалённый адрес (это и есть «куда стучится»).
                var pid = ResolvePid(connection.LocalEndPoint.Address.ToString(), connection.LocalEndPoint.Port, tcpOwners);
                connections.Add(new ActiveConnection
                {
                    ProcessName = ResolveProcessName(pid, nameByPid),
                    ProcessId = pid,
                    RemoteAddress = address,
                    RemotePort = port,
                });
            }
        }
        catch (Exception)
        {
            // Список подключений недоступен — пусто.
        }

        return connections;
    }

    // netstat печатает IPv4 как «a.b.c.d:port», IPv6 как «[::1]:port» — пробуем обе формы ключа. 0 — не нашли.
    private static int ResolvePid(string address, int port, IReadOnlyDictionary<string, int> owners) =>
        owners.TryGetValue($"{address}:{port}", out var pid) || owners.TryGetValue($"[{address}]:{port}", out pid)
            ? pid
            : 0;

    private static string ResolveProcessName(int pid, Dictionary<int, string> nameByPid)
    {
        if (pid == 0)
        {
            return string.Empty;
        }

        if (nameByPid.TryGetValue(pid, out var cached))
        {
            return cached;
        }

        var name = TryGetProcessName(pid);
        nameByPid[pid] = name;
        return name;
    }

    private static string TryGetProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (Exception)
        {
            // Процесс завершился / недоступен — имя неизвестно.
            return string.Empty;
        }
    }
}
