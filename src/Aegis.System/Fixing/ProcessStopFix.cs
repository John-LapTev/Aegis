using System.Diagnostics;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Fixing;

/// <summary>
/// Останавливает процесс по PID. Это «мягкое» действие: файл программы остаётся на месте, процесс можно
/// запустить снова — поэтому отдельный бэкап не создаётся (точка восстановления делается оркестратором).
/// </summary>
public sealed class ProcessStopFix : IFix
{
    private readonly int _processId;

    public ProcessStopFix(string findingId, int processId)
    {
        FindingId = findingId;
        _processId = processId;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Processes;

    // Критичные системные процессы: никогда не завершаем, даже если сканер ошибочно их пометил (защита в глубину,
    // как в ForceDeleteService — аудит 2026-07-04). Их остановка роняет Windows.
    private static readonly global::System.Collections.Generic.HashSet<string> CriticalProcesses =
        new(global::System.StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon",
            "services", "lsass", "lsaiso", "fontdrvhost", "dwm",
        };

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.GetProcessById(_processId);

            // Предохранитель: системные PID (0/4) и КРИТИЧНЫЕ ИМЕНА из System32 не трогаем — их завершение уронит
            // систему. НО тот же по имени процесс (напр. «lsass»), запущенный НЕ из System32 (из %TEMP%/папки
            // пользователя), — это маскирующийся вредонос: его останавливать МОЖНО (аудит 2026-07-04).
            if (_processId is 0 or 4
                || (CriticalProcesses.Contains(process.ProcessName) && IsSystemImage(process)))
            {
                return Task.FromResult(FixOutcome.Failed(
                    "Это критичный системный процесс — останавливать его нельзя (Windows перестанет работать)."));
            }

            // entireProcessTree: убиваем и дочерние процессы. Важно для майнеров/вредоносов: у них часто
            // есть процесс-«сторож» (watchdog), который перезапускает основной — убив только один, мы бы его
            // не остановили. Точку восстановления/бэкап тут не делаем (действие мягкое, файл на месте).
            process.Kill(entireProcessTree: true);
            // Честно сообщаем, если процесс не завершился за отведённое время (защищён/перезапускается) — иначе
            // рапортовали бы «остановлен», хотя он ещё жив (аудит 2026-07-04).
            if (!process.WaitForExit(3000))
            {
                return Task.FromResult(FixOutcome.Failed(
                    "Процесс не удалось остановить — возможно, его защищает Windows или он сразу перезапускается."));
            }

            return Task.FromResult(FixOutcome.OkWithoutBackup());
        }
        catch (ArgumentException)
        {
            // Процесс уже не запущен — цель достигнута.
            return Task.FromResult(FixOutcome.OkWithoutBackup());
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось остановить процесс: " + ex.Message));
        }
    }

    /// <summary>
    /// Лежит ли образ процесса в системной папке (System32). Настоящие критичные процессы — там; если путь прочитать
    /// нельзя (обычно у защищённых системных процессов) — тоже считаем системным (не трогаем). Маскирующийся вредонос
    /// с тем же именем читается свободно и лежит вне System32 → вернём false → его можно остановить.
    /// </summary>
    private static bool IsSystemImage(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            var system32 = global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.System);
            return path.StartsWith(system32, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return true; // нет доступа к MainModule — это признак настоящего защищённого системного процесса
        }
    }
}
