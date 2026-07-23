namespace Aegis.System.Internal;

/// <summary>
/// Разбор вывода <c>netstat -ano</c> в карту «удалённый адрес:порт → PID владельца». Managed-API
/// (GetActiveTcpConnections) не отдаёт PID, поэтому имя программы для подключения берём отсюда.
/// Локаль-устойчиво: опираемся только на неперевод имые токены (<c>TCP</c> и числовой PID),
/// не на локализованное слово состояния. Чистая функция — тестируется на любой ОС.
/// </summary>
internal static class NetstatParser
{
    /// <summary>
    /// Карта «удалённая точка (как в netstat) → PID». Берём только TCP-строки.
    /// Формат строки TCP: <c>Proto  LocalAddr  ForeignAddr  State  PID</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, int> ParseTcpPidMap(string netstatOutput)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(netstatOutput))
        {
            return map;
        }

        foreach (var line in netstatOutput.Split('\n'))
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            // TCP-строка: первый токен «TCP», последний — числовой PID, всего не меньше 5 колонок.
            if (tokens.Length < 5 || !tokens[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(tokens[^1], out var pid))
            {
                continue;
            }

            // tokens[1] — ЛОКАЛЬНЫЙ адрес:порт. Он уникален для каждого активного TCP-подключения, поэтому
            // PID не схлопывается (раньше ключом был удалённый адрес — два сокета к одному серверу затирали друг
            // друга, и «Остановить» мог убить НЕ тот процесс). Поиск тоже по локальной точке.
            map[tokens[1]] = pid;
        }

        return map;
    }

    /// <summary>
    /// Порты, которые ЖДУТ входящих подключений и доступны снаружи. Локальные (127.0.0.1 и [::1]) не в счёт:
    /// к ним из сети не подключиться, и пугать ими человека нечем. Чистая функция — тестируется на любой ОС.
    /// </summary>
    public static IReadOnlyList<int> ParseListeningPorts(string netstatOutput)
    {
        var ports = new SortedSet<int>();
        if (string.IsNullOrEmpty(netstatOutput))
        {
            return [];
        }

        foreach (var line in netstatOutput.Split('\n'))
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            // Строка прослушивания: TCP  <локальный адрес:порт>  <удалённый>  LISTENING  <PID>.
            // Слово состояния локализовано не бывает (netstat печатает его латиницей), но опираемся на форму:
            // у слушающего сокета удалённый адрес — заглушка вида 0.0.0.0:0 или [::]:0.
            if (tokens.Length < 4 || !tokens[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remote = tokens[2];
            if (!remote.EndsWith(":0", StringComparison.Ordinal))
            {
                continue;
            }

            var local = tokens[1];
            var separator = local.LastIndexOf(':');
            if (separator <= 0 || !int.TryParse(local[(separator + 1)..], out var port) || port <= 0)
            {
                continue;
            }

            var address = local[..separator].Trim('[', ']');
            if (address is "127.0.0.1" or "::1")
            {
                continue;
            }

            ports.Add(port);
        }

        return ports.ToList();
    }
}
