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

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.GetProcessById(_processId);
            // entireProcessTree: убиваем и дочерние процессы. Важно для майнеров/вредоносов: у них часто
            // есть процесс-«сторож» (watchdog), который перезапускает основной — убив только один, мы бы его
            // не остановили. Точку восстановления/бэкап тут не делаем (действие мягкое, файл на месте).
            process.Kill(entireProcessTree: true);
            process.WaitForExit(3000);
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
}
