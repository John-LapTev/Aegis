using System.Globalization;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Threats;

/// <summary>
/// Поведенческий детект скрытых майнеров (группа <see cref="ScanGroup.Threats"/>). Обычный сканер процессов
/// смотрит на один признак; этот СОБИРАЕТ поведенческий портрет майнера: программа без подписи ПОСТОЯННО грузит
/// процессор И при этом прячется в скрытой папке / закреплена в автозапуске / со странным именем / грузит комп,
/// пока человек отошёл. Несколько признаков вместе — это уверенный вывод «скрытый майнер», а не догадка.
/// Подписанные программы (игры, кодировщики, рендер) НЕ трогаем — у них высокая нагрузка законна.
/// </summary>
public sealed class MinerBehaviorScanner : IScanner
{
    /// <summary>Простой (без ввода) от которого считаем, что человек отошёл — нагрузка в это время подозрительна.</summary>
    private static readonly TimeSpan AwayThreshold = TimeSpan.FromMinutes(5);

    private const string Section = "Поведение процессов (майнеры)";

    private readonly IProcessProbe _processProbe;
    private readonly IAutostartProbe _autostartProbe;
    private readonly IUserActivityProbe _activityProbe;

    public MinerBehaviorScanner(IProcessProbe processProbe, IAutostartProbe autostartProbe, IUserActivityProbe activityProbe)
    {
        ArgumentNullException.ThrowIfNull(processProbe);
        ArgumentNullException.ThrowIfNull(autostartProbe);
        ArgumentNullException.ThrowIfNull(activityProbe);
        _processProbe = processProbe;
        _autostartProbe = autostartProbe;
        _activityProbe = activityProbe;
    }

    public ScanGroup Group => ScanGroup.Threats;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var processes = await _processProbe.FindAsync(cancellationToken).ConfigureAwait(false);
        var autostart = await _autostartProbe.FindAsync(cancellationToken).ConfigureAwait(false);
        var idle = _activityProbe.GetIdleDuration();
        var userAway = idle >= AwayThreshold;

        var findings = new List<Finding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in processes)
        {
            // Кандидат — только неподписанная программа, которая РЕАЛЬНО грузит процессор.
            // Подписанные (игры, рендер, кодировщики) с высокой нагрузкой — это законно, их пропускаем.
            if (process.CpuPercent < MinerHeuristics.CpuGate || !MinerHeuristics.IsUntrusted(process.Signature))
            {
                continue;
            }

            // Один и тот же файл несколькими копиями — одной находкой.
            var key = string.IsNullOrEmpty(process.ExecutablePath) ? process.Name : process.ExecutablePath;
            if (!seen.Add(key))
            {
                continue;
            }

            var reasons = new List<string>
            {
                $"постоянно грузит процессор (~{process.CpuPercent:0}%)",
                "у программы нет подтверждённой цифровой подписи",
            };

            var corroborating = 0;

            if (MinerHeuristics.IsStealthPath(process.ExecutablePath))
            {
                corroborating++;
                reasons.Add("запускается из скрытой служебной папки (временной или AppData), откуда обычные программы не работают");
            }

            if (IsPersistent(process, autostart))
            {
                corroborating++;
                reasons.Add("прописана в автозапуске — стартует сама при каждом включении компьютера");
            }

            if (MinerHeuristics.LooksRandomName(process.Name))
            {
                corroborating++;
                reasons.Add("у файла бессмысленное «случайное» имя — так часто маскируются вирусы");
            }

            if (userAway)
            {
                corroborating++;
                reasons.Add("нагрузка идёт, пока вы не за компьютером, — типичное поведение скрытого майнера");
            }

            if (corroborating == 0)
            {
                continue; // просто неподписанное + нагрузка — это уже покажет вкладка «Процессы», здесь не дублируем
            }

            var severity = corroborating >= 2 ? Severity.Danger : Severity.Warning;
            findings.Add(BuildFinding(process, severity, reasons));
        }

        if (findings.Count == 0)
        {
            findings.Add(new Finding
            {
                Id = "miner-behavior-none",
                Group = ScanGroup.Threats,
                Severity = Severity.Ok,
                Title = "Скрытых майнеров по поведению не нашёл",
                Explain = "Aegis проверил, нет ли программы, которая ведёт себя как скрытый майнер (без подписи, грузит " +
                          "процессор, прячется в служебных папках, сама прописалась в автозапуск). Ничего такого нет — это хорошо.",
                Data = new Dictionary<string, string> { ["section"] = Section },
            });
        }

        return new ScanResult { Group = ScanGroup.Threats, Findings = findings };
    }

    private static Finding BuildFinding(ProcessInfo process, Severity severity, List<string> reasons)
    {
        var verdict = severity == Severity.Danger
            ? "Очень похоже на скрытый майнер"
            : "Процесс ведёт себя как скрытый майнер";

        var body = severity == Severity.Danger
            ? "Совпало сразу несколько признаков скрытого майнера криптовалюты — программы, которая тайком грузит ваш " +
              "компьютер, чтобы зарабатывать для чужого человека. Из-за неё компьютер тормозит, греется и «ест» больше " +
              "электричества. Стоит остановить процесс и проверить файл (Защитник Windows + VirusTotal). Перед действиями — бэкап."
            : "Программа частично ведёт себя как скрытый майнер. Это не точно вирус, но повод проверить: остановить " +
              "процесс и сверить файл онлайн (Защитник Windows + VirusTotal).";

        var explain = body + " Почему так решили: " + string.Join("; ", reasons) + ".";

        var data = new Dictionary<string, string>
        {
            ["section"] = Section,
            ["kind"] = FindingKinds.ProcessStop,
            ["pid"] = process.ProcessId.ToString(CultureInfo.InvariantCulture),
            ["name"] = process.Name,
        };
        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
        {
            data["path"] = process.ExecutablePath;
        }

        return new Finding
        {
            Id = $"miner-behavior-{process.Name}-{process.ProcessId}",
            Group = ScanGroup.Threats,
            Severity = severity,
            Title = verdict,
            Detail = process.ExecutablePath,
            Explain = explain,
            Data = data,
        };
    }

    /// <summary>Процесс закреплён в автозапуске (по совпадению пути или имени в команде запуска).</summary>
    private static bool IsPersistent(ProcessInfo process, IReadOnlyList<AutostartEntry> autostart)
    {
        foreach (var entry in autostart)
        {
            if (string.IsNullOrWhiteSpace(entry.Command))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(process.ExecutablePath)
                && entry.Command.Contains(process.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // По имени — только если оно достаточно характерное (иначе пустое/короткое имя матчит любую команду
            // и ложно раздувает вердикт «майнер»). Сверяем полное имя exe.
            var exeName = process.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process.Name : process.Name + ".exe";
            if (exeName.Length >= 8 && entry.Command.Contains(exeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
