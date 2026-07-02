namespace Aegis.Scanners.Internal;

/// <summary>
/// Эвристики для сетевых угроз: распознавание «чёрной дыры» в hosts, частных/доверенных адресов,
/// доменов защиты/обновлений и «высокоценных» доменов (банки, почта). Строковые эвристики без сети.
/// </summary>
internal static class NetworkHeuristics
{
    // Адреса, на которые вредонос перенаправляет домен, чтобы «убить» его (заблокировать).
    private static readonly string[] BlackholeIps = ["0.0.0.0", "127.0.0.1", "::1", "::"];

    // Префиксы частных/локальных адресов (DNS оттуда — обычно роутер, это нормально).
    private static readonly string[] PrivatePrefixes =
        ["127.", "10.", "192.168.", "169.254.", "172.16.", "172.17.", "172.18.", "172.19.",
         "172.20.", "172.21.", "172.22.", "172.23.", "172.24.", "172.25.", "172.26.", "172.27.",
         "172.28.", "172.29.", "172.30.", "172.31.", "::1", "fe80", "fc", "fd"];

    // Известные публичные DNS-резолверы (включая популярные в РФ — Яндекс).
    private static readonly HashSet<string> KnownPublicResolvers = new(StringComparer.OrdinalIgnoreCase)
    {
        "8.8.8.8", "8.8.4.4",            // Google
        "1.1.1.1", "1.0.0.1",            // Cloudflare
        "9.9.9.9", "149.112.112.112",    // Quad9
        "208.67.222.222", "208.67.220.220", // OpenDNS
        "94.140.14.14", "94.140.15.15",  // AdGuard
        "77.88.8.8", "77.88.8.1",        // Яндекс
    };

    // Домены защиты/обновлений: блокировка их в hosts — типичный приём вредоносного ПО.
    // ВАЖНО: без общего "microsoft.com" — популярные антирекламные hosts-списки (StevenBlack и т.п.) блокируют
    // десятки телеметрийных *.microsoft.com, и общий фрагмент давал бы красное «Заблокирована защита» на каждый
    // (ложная тревога, правка аудита). Ловим только КОНКРЕТНЫЕ домены обновлений/защиты.
    private static readonly string[] SecurityOrUpdateDomains =
        ["windowsupdate", "update.microsoft", "sls.update.microsoft", "delivery.mp.microsoft",
         "definitionupdates", "ctldl.windowsupdate", "defender", "windowsdefender",
         "smartscreen", "mpcmdrun", "wns.windows",
         "kaspersky", "avast", "avg", "drweb", "eset", "nod32", "malwarebytes",
         "virustotal", "norton", "mcafee", "bitdefender", "avira", "sophos"];

    // Серверы активации/лицензий: их блокировка обычно дело кейгенов/взломщиков (часто с вирусом).
    private static readonly string[] ActivationDomains =
        ["activation.sls.microsoft", "genuine.microsoft", "validation.sls.microsoft",
         "lm.licenses.adobe", "practivate.adobe", "activate.adobe", "hl2rcv.adobe", "adobe-dns",
         "licens", "activation"];

    // «Высокоценные» домены: подмена их адреса в hosts — фишинг/кража.
    private static readonly string[] HighValueDomains =
        ["google.com", "gmail.com", "paypal", "sberbank", "tinkoff", "alfabank", "vtb",
         "gosuslugi", "vk.com", "yandex.ru", "mail.ru", "facebook.com", "instagram.com", "binance"];

    public static bool IsBlackhole(string? ip) =>
        ip is not null && Array.Exists(BlackholeIps, b => string.Equals(b, ip.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsPrivateOrLoopback(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return false;
        }

        var value = ip.Trim().ToLowerInvariant();
        return Array.Exists(PrivatePrefixes, p => value.StartsWith(p, StringComparison.Ordinal));
    }

    public static bool IsKnownPublicResolver(string? ip) =>
        ip is not null && KnownPublicResolvers.Contains(ip.Trim());

    public static bool IsSecurityOrUpdateDomain(string? host) => ContainsAny(host, SecurityOrUpdateDomains);

    public static bool IsHighValueDomain(string? host) => ContainsAny(host, HighValueDomains);

    // Типичные порты майнинг-пулов (Stratum) и сети Tor.
    // Известные stratum-порты майнинг-пулов (из конфигов майнеров/пулов). Только специфичные для майнинга —
    // НЕ добавляем 80/443/8080 и подобные, чтобы не ловить легитимные сервисы. Расширено по базам пулов.
    private static readonly HashSet<int> MiningPoolPorts =
        [3333, 4444, 5555, 7777, 8888, 9999, 14444, 45560, 45700, 3032, 5730,
         14433, 3334, 3335, 17777, 20535, 1314, 9980, 45690, 45750];

    private static readonly HashSet<int> TorPorts = [9001, 9030, 9050, 9051, 9150];

    public static bool IsMiningPoolPort(int port) => MiningPoolPorts.Contains(port);

    public static bool IsTorPort(int port) => TorPorts.Contains(port);

    public static bool IsActivationDomain(string? host) => ContainsAny(host, ActivationDomains);

    /// <summary>Публичный адрес (не «чёрная дыра» и не частный/локальный) — внешний сервер.</summary>
    public static bool IsPublicAddress(string? ip) => !IsBlackhole(ip) && !IsPrivateOrLoopback(ip);

    private static bool ContainsAny(string? host, string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var value = host.ToLowerInvariant();
        return Array.Exists(fragments, f => value.Contains(f, StringComparison.Ordinal));
    }
}
