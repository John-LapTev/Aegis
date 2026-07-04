using System.Globalization;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Processes;

/// <summary>
/// Сканер активных процессов (группа <see cref="ScanGroup.Processes"/>). Заранее «знает» процессы Windows и
/// известных вендоров (видеокарта/звук) по издателю из подписи и помечает их безопасными с понятной подписью
/// происхождения. Системные процессы Windows и прочие подписанные программы сворачивает в сводку, чтобы не
/// плодить сотни строк. Неподписанные/неизвестные показывает как «Внимание» (их подтвердит онлайн-проверка),
/// а запуск из временной папки или высокую нагрузку без подписи — как «Проблема» (возможный вирус/майнер).
/// </summary>
public sealed class ProcessesScanner : IScanner
{
    /// <summary>
    /// Порог нагрузки CPU (% от ВСЕЙ мощности процессора), выше которого неподписанный процесс считаем
    /// возможным майнером. 30% от всех ядер — заметная постоянная нагрузка для фонового процесса.
    /// </summary>
    private const double HighCpuPercent = 30d;

    private readonly IProcessProbe _probe;

    public ProcessesScanner(IProcessProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Processes;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var processes = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = new List<Finding>();
        var windowsCount = 0;
        var otherSignedCount = 0;
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in processes)
        {
            // Дедуп: один и тот же файл, запущенный несколькими копиями (напр. NVDisplay.Container ×2),
            // показываем одной строкой. Процессы без пути (защищённые) не схлопываем.
            if (!string.IsNullOrEmpty(process.ExecutablePath) && !seenPaths.Add(process.ExecutablePath))
            {
                continue;
            }

            var (origin, label) = ProgramCatalog.Classify(process.Publisher, process.Signature);

            // Защита от ложного «без подписи»: у защищённых системных файлов (winlogon, dwm, spoolsv…)
            // подпись с реальной системы иногда не читается — но это процессы самой Windows из папки
            // Windows. Считаем их системными, чтобы не пугать пользователя ложной тревогой.
            if (origin is not ProgramOrigin.Windows && IsKnownWindowsProcess(process))
            {
                origin = ProgramOrigin.Windows;
            }

            switch (origin)
            {
                case ProgramOrigin.Windows:
                    windowsCount++;
                    break;
                case ProgramOrigin.OtherSigned:
                    otherSignedCount++;
                    break;
                case ProgramOrigin.HardwareVendor:
                    findings.Add(SafeFinding(process, label));
                    break;
                default:
                    findings.Add(SuspiciousFinding(process, label));
                    break;
            }
        }

        if (windowsCount > 0)
        {
            findings.Add(SummaryFinding("processes-windows-summary", windowsCount,
                $"Системные процессы Windows: {windowsCount}",
                "Это процессы самой Windows — все с цифровой подписью Microsoft. Они нужны для работы системы " +
                "и полностью безопасны, трогать их не нужно."));
        }

        if (otherSignedCount > 0)
        {
            findings.Add(SummaryFinding("processes-signed-summary", otherSignedCount,
                $"Программы с подписью: {otherSignedCount}",
                "Это обычные программы с подтверждённой цифровой подписью (браузеры, игры, мессенджеры и т.п.). " +
                "Подпись подтверждает издателя — таким программам можно доверять."));
        }

        return new ScanResult { Group = ScanGroup.Processes, Findings = findings };
    }

    /// <summary>Известная безопасная программа вендора (видеокарта и т.п.) — зелёным, с подписью происхождения.</summary>
    private static Finding SafeFinding(ProcessInfo process, string label) => new()
    {
        Id = $"process-safe-{process.Name}-{process.ProcessId}",
        Group = ScanGroup.Processes,
        Severity = Severity.Ok,
        Title = process.Name,
        Detail = process.ExecutablePath,
        Explain = $"Это «{label}» — программа с цифровой подписью, ей можно доверять. Ничего делать не нужно.",
        Data = new Dictionary<string, string> { ["category"] = label },
    };

    private static Finding SuspiciousFinding(ProcessInfo process, string label)
    {
        var (severity, title, explain) = Classify(process);
        var data = new Dictionary<string, string>
        {
            [FindingDataKeys.Kind] = FindingKinds.ProcessStop,
            ["pid"] = process.ProcessId.ToString(CultureInfo.InvariantCulture),
            ["name"] = process.Name,
            ["category"] = label,
        };
        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
        {
            data["path"] = process.ExecutablePath;
        }

        return new Finding
        {
            Id = $"process-{process.Name}-{process.ProcessId}",
            Group = ScanGroup.Processes,
            Severity = severity,
            Title = title,
            Detail = process.ExecutablePath,
            Explain = explain,
            Data = data,
        };
    }

    private static (Severity Severity, string Title, string Explain) Classify(ProcessInfo process)
    {
        var suspicious = PathHeuristics.IsSuspiciousLocation(process.ExecutablePath);

        if (process.Signature == SignatureStatus.Unsigned && suspicious)
        {
            return (
                Severity.Danger,
                "Подозрительный процесс из временной папки",
                "Запущена программа без подписи прямо из временной папки. Так ведут себя вирусы и майнеры. " +
                "Стоит остановить процесс и проверить файл (мы сверим его репутацию онлайн). Перед действиями — бэкап.");
        }

        if (process.CpuPercent >= HighCpuPercent && process.Signature != SignatureStatus.Signed)
        {
            return (
                Severity.Danger,
                "Возможный майнер: высокая нагрузка на процессор",
                "Программа без подтверждённой подписи постоянно сильно грузит процессор. Так часто работают " +
                "скрытые майнеры криптовалюты — компьютер тормозит и греется. Проверим её и при необходимости остановим.");
        }

        return (
            Severity.Warning,
            "Процесс без цифровой подписи",
            "У запущенной программы нет подтверждённого издателя. Это не обязательно вирус — проверим её онлайн " +
            "(Защитник Windows + VirusTotal); если всё чисто, пометим зелёным.");
    }

    private static Finding SummaryFinding(string id, int count, string title, string explain) => new()
    {
        Id = id,
        Group = ScanGroup.Processes,
        Severity = Severity.Ok,
        Title = title,
        Detail = $"проверено: {count}",
        Explain = explain,
    };

    /// <summary>Известные процессы самой Windows (защита от ложного «без подписи» у защищённых системных файлов).</summary>
    private static readonly HashSet<string> KnownWindowsProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "winlogon.exe", "wininit.exe", "csrss.exe", "smss.exe", "services.exe", "lsass.exe", "lsaiso.exe",
        "svchost.exe", "dwm.exe", "spoolsv.exe", "explorer.exe", "taskhostw.exe", "ctfmon.exe",
        "fontdrvhost.exe", "RuntimeBroker.exe", "SearchIndexer.exe", "SearchHost.exe", "SearchApp.exe",
        "StartMenuExperienceHost.exe", "ShellExperienceHost.exe", "sihost.exe", "conhost.exe", "dllhost.exe",
        "WmiPrvSE.exe", "audiodg.exe", "SystemSettings.exe", "SecurityHealthService.exe",
        "SecurityHealthSystray.exe", "dasHost.exe", "WUDFHost.exe", "TextInputHost.exe", "smartscreen.exe",
        "ApplicationFrameHost.exe", "LockApp.exe", "UserOOBEBroker.exe", "wlanext.exe",
    };

    /// <summary>Это процесс самой Windows? По имени файла + расположению в папке Windows (для случаев,
    /// когда подпись защищённого системного файла не читается и он ошибочно выглядит «без подписи»).</summary>
    private static bool IsKnownWindowsProcess(ProcessInfo process)
    {
        // Имя файла извлекаем вручную: и '\' (Windows), и '/' — чтобы работало и при сборке/тестах на Linux.
        var fileName = process.Name;
        if (!string.IsNullOrEmpty(process.ExecutablePath))
        {
            var slash = process.ExecutablePath.LastIndexOfAny(['\\', '/']);
            fileName = slash >= 0 ? process.ExecutablePath[(slash + 1)..] : process.ExecutablePath;
        }

        var normalized = fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".exe";
        if (!KnownWindowsProcesses.Contains(normalized))
        {
            return false;
        }

        // Без пути (защищённый системный процесс) — достаточно имени; с путём — он должен быть в папке Windows.
        if (string.IsNullOrEmpty(process.ExecutablePath))
        {
            return true;
        }

        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return string.IsNullOrEmpty(windowsDir)
               || process.ExecutablePath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase);
    }
}
