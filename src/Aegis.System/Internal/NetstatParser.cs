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
}
