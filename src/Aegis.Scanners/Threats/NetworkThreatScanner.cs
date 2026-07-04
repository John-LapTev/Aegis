using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Threats;

/// <summary>
/// Сетевые угрозы (группа <see cref="ScanGroup.Threats"/>): подмена/блокировка в файле hosts,
/// нестандартный DNS, подозрительные подключения процессов. Тексты — простыми словами с вердиктом.
/// </summary>
public sealed class NetworkThreatScanner : IScanner
{
    /// <summary>Выше этого числа записей файл hosts считаем ненормально большим (раздутым).</summary>
    private const int HostsBloatThreshold = 1000;

    private readonly INetworkThreatProbe _probe;

    public NetworkThreatScanner(INetworkThreatProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Threats;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);

        var findings = new List<Finding>();

        var bloated = snapshot.HostsEntries.Count > HostsBloatThreshold;
        foreach (var entry in snapshot.HostsEntries)
        {
            var finding = ClassifyHostsEntry(entry);
            // В раздутом hosts не засыпаем пользователя сотнями «Info» — оставляем только важное.
            if (bloated && finding.Severity == Severity.Info)
            {
                continue;
            }

            findings.Add(finding);
        }

        if (bloated)
        {
            findings.Add(CreateHostsBloatFinding(snapshot.HostsEntries.Count));
        }

        findings.AddRange(snapshot.DnsServers.Where(IsSuspiciousDns).Select(CreateDnsFinding));
        findings.AddRange(snapshot.SuspiciousConnections.Select(CreateConnectionFinding));

        foreach (var connection in snapshot.ActiveConnections)
        {
            var finding = ClassifyActiveConnection(connection);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        return new ScanResult { Group = ScanGroup.Threats, Findings = findings };
    }

    private static Finding? ClassifyActiveConnection(ActiveConnection connection)
    {
        if (NetworkHeuristics.IsMiningPoolPort(connection.RemotePort))
        {
            // Если знаем PID — даём реальную кнопку «Остановить» (через ProcessStopFix). Иначе только показываем.
            var canStop = connection.ProcessId > 0;
            return new Finding
            {
                Id = $"threat-port-{connection.RemoteAddress}-{connection.RemotePort}-{connection.ProcessId}",
                Group = ScanGroup.Threats,
                Severity = Severity.Danger,
                Title = "Подключение к майнинг-пулу",
                Detail = $"{ProgramLabel(connection.ProcessName)} → {connection.RemoteAddress}:{connection.RemotePort}",
                Explain = "Программа на компьютере подключается к серверу для майнинга криптовалюты. Так работают " +
                          "скрытые майнеры — компьютер тормозит, греется и зарабатывает деньги чужому. " +
                          (canStop
                              ? "Нажми «Остановить» — программа будет завершена."
                              : "Найди эту программу во вкладке «Процессы» и останови её."),
                Data = canStop
                    ? new Dictionary<string, string>
                    {
                        [FindingDataKeys.Kind] = FindingKinds.ProcessStop,
                        ["pid"] = connection.ProcessId.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
                    }
                    : null,
            };
        }

        if (NetworkHeuristics.IsTorPort(connection.RemotePort))
        {
            return new Finding
            {
                Id = $"threat-port-{connection.RemoteAddress}-{connection.RemotePort}-{connection.ProcessId}",
                Group = ScanGroup.Threats,
                Severity = Severity.Warning,
                Title = "Подключение через анонимную сеть Tor",
                Detail = $"{ProgramLabel(connection.ProcessName)} → {connection.RemoteAddress}:{connection.RemotePort}",
                Explain = "Программа выходит в интернет через анонимную сеть Tor, скрывая, куда именно. Иногда это " +
                          "легально, но так же прячутся вирусы. Если ты не пользуешься Tor намеренно — стоит проверить.",
            };
        }

        return null;
    }

    private static Finding CreateHostsBloatFinding(int count) => new()
    {
        Id = "threat-hosts-bloat",
        Group = ScanGroup.Threats,
        Severity = Severity.Warning,
        Title = "Файл hosts необычно большой",
        Detail = $"записей: {count}",
        Explain = "В системном файле hosts очень много записей — вручную столько не добавляют. Обычно это " +
                  "«блокировщик» сомнительного происхождения или вирус, перенаправляющий множество сайтов. Стоит проверить.",
    };

    private static Finding ClassifyHostsEntry(HostsEntry entry)
    {
        var (severity, title, explain) = ClassifyHosts(entry);

        return new Finding
        {
            Id = $"threat-hosts-{entry.Hostname}-{entry.MappedIp}",
            Group = ScanGroup.Threats,
            Severity = severity,
            Title = title,
            Detail = $"{entry.Hostname} → {entry.MappedIp}",
            Explain = explain,
        };
    }

    private static (Severity Severity, string Title, string Explain) ClassifyHosts(HostsEntry entry)
    {
        var isBlackhole = NetworkHeuristics.IsBlackhole(entry.MappedIp);

        if (isBlackhole && NetworkHeuristics.IsSecurityOrUpdateDomain(entry.Hostname))
        {
            return (
                Severity.Danger,
                "Заблокирована защита или обновления (файл hosts)",
                "Кто-то заблокировал обновления Windows или антивирус через системный файл hosts — так вирусы " +
                "прячутся, чтобы их не нашли и не удалили. Это опасно: проверь компьютер антивирусом и убери эту " +
                "запись из файла hosts (или покажи результат тому, кто разбирается).");
        }

        if (isBlackhole && NetworkHeuristics.IsActivationDomain(entry.Hostname))
        {
            return (
                Severity.Warning,
                "Заблокированы серверы активации (возможно, взломанный софт)",
                "Заблокированы серверы проверки лицензий — так делают программы для взлома платных программ. " +
                "Вместе с таким софтом часто приходит вирус. Стоит вспомнить, что устанавливалось, и проверить компьютер.");
        }

        if (isBlackhole && NetworkHeuristics.IsHighValueDomain(entry.Hostname))
        {
            return (
                Severity.Danger,
                "Заблокирован доступ к важному сайту (файл hosts)",
                "Доступ к важному сайту (банк, почта, госуслуги) заблокирован в системном файле hosts. Так может " +
                "действовать вредонос, мешая войти в личный кабинет или сменить пароль. Это срочно: проверь компьютер " +
                "антивирусом и убери эту запись из файла hosts (или обратись к тому, кто разбирается).");
        }

        if (!isBlackhole && NetworkHeuristics.IsHighValueDomain(entry.Hostname))
        {
            return (
                Severity.Danger,
                "Подмена адреса важного сайта (файл hosts)",
                "Адрес важного сайта (банк, почта, госуслуги) подменён в системном файле — можно попасть на " +
                "поддельную копию сайта и отдать мошенникам пароли/деньги. Это срочно: не входи в этот сайт, проверь " +
                "компьютер антивирусом и убери подмену из файла hosts (или обратись к тому, кто разбирается).");
        }

        if (NetworkHeuristics.IsPublicAddress(entry.MappedIp))
        {
            return (
                Severity.Warning,
                "Трафик сайта идёт через чужой сервер (файл hosts)",
                "Адрес сайта подменён в системном файле так, что трафик идёт через чужой сервер в интернете. " +
                "Иногда так вирус перехватывает данные. Если ты это не настраивал — лучше убрать.");
        }

        return (
            Severity.Info,
            "Изменение в файле hosts",
            "В системном файле hosts есть ручная запись. Часто это безвредно (например, блокировка рекламы). " +
            "Но если ты её не добавлял — лучше убрать, на всякий случай.");
    }

    private static bool IsSuspiciousDns(string server) =>
        !NetworkHeuristics.IsPrivateOrLoopback(server) && !NetworkHeuristics.IsKnownPublicResolver(server);

    private static Finding CreateDnsFinding(string server) => new()
    {
        Id = $"threat-dns-{server}",
        Group = ScanGroup.Threats,
        Severity = Severity.Warning,
        Title = "Нестандартный DNS-сервер",
        Detail = server,
        Explain = "Интернет настроен на необычный DNS-сервер. Чаще всего это нормально — так делают VPN " +
                  "(например, Cloudflare WARP), антивирусы или твои собственные настройки. Реже так поступают вирусы. " +
                  "Если это твой VPN/настройка — нажми «Безопасно», и пункт станет зелёным и больше не побеспокоит.",
    };

    private static Finding CreateConnectionFinding(SuspiciousConnection connection)
    {
        var canStop = connection.ProcessId > 0;
        return new Finding
        {
            Id = $"threat-port-conn-{connection.RemoteAddress}-{connection.RemotePort}-{connection.ProcessId}",
            Group = ScanGroup.Threats,
            Severity = Severity.Danger,
            Title = "Подозрительное сетевое подключение",
            Detail = $"{ProgramLabel(connection.ProcessName)} → {connection.RemoteAddress}:{connection.RemotePort} ({connection.Reason})",
            Explain = "Программа на компьютере подключается к подозрительному адресу в интернете. Так ведут себя " +
                      "трояны (передают твои данные) и майнеры (общаются со своим сервером). " +
                      (canStop ? "Нажми «Остановить», чтобы завершить её." : "Останови её во вкладке «Процессы»."),
            Data = canStop
                ? new Dictionary<string, string>
                {
                    [FindingDataKeys.Kind] = FindingKinds.ProcessStop,
                    ["pid"] = connection.ProcessId.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
                }
                : null,
        };
    }

    /// <summary>Имя программы для показа: реальное или «неизвестная программа», если PID не сопоставлен.</summary>
    private static string ProgramLabel(string processName) =>
        string.IsNullOrWhiteSpace(processName) ? "неизвестная программа" : processName;
}
