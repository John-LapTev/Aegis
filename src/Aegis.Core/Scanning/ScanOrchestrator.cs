using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Core.Scanning;

/// <summary>
/// Последовательно запускает зарегистрированные сканеры, сообщает прогресс и собирает результаты.
/// Если отдельный сканер падает — превращает ошибку в видимую находку (а не роняет весь скан),
/// чтобы пользователь увидел проблему (coding-standards: ошибка доходит до UI).
/// </summary>
public sealed class ScanOrchestrator : IScanOrchestrator
{
    private readonly IReadOnlyList<IScanner> _scanners;

    public ScanOrchestrator(IEnumerable<IScanner> scanners)
    {
        ArgumentNullException.ThrowIfNull(scanners);
        _scanners = scanners.ToList();
    }

    public async Task<IReadOnlyList<ScanResult>> ScanAllAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var total = _scanners.Count;
        var results = new ScanResult[total];
        if (total == 0)
        {
            return results;
        }

        // Сканеры — независимые stateless-синглтоны, поэтому запускаем их ПАРАЛЛЕЛЬНО (с ограничением, чтобы
        // не перегрузить диск/WMI). Полная проверка идёт за время самого медленного, а не за сумму всех.
        // Отчёт «сканер завершён» отправляем сразу — UI наполняет вкладку, не дожидаясь остальных.
        var remainingByGroup = _scanners.GroupBy(s => s.Group).ToDictionary(g => g.Key, g => g.Count());
        var progressLock = new object();
        var findingsSoFar = 0;
        var completed = 0;
        using var gate = new SemaphoreSlim(Math.Min(MaxParallelScanners, total));

        var tasks = new Task[total];
        for (var i = 0; i < total; i++)
        {
            var index = i;
            var scanner = _scanners[index];
            tasks[index] = Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var result = await RunSafelyAsync(scanner, cancellationToken).ConfigureAwait(false);
                    results[index] = result;

                    bool groupDone;
                    int soFar;
                    int done;
                    lock (progressLock)
                    {
                        findingsSoFar += result.Findings.Count;
                        soFar = findingsSoFar;
                        done = ++completed;
                        remainingByGroup[scanner.Group]--;
                        groupDone = remainingByGroup[scanner.Group] == 0;
                    }

                    progress?.Report(new ScanProgress
                    {
                        Current = scanner.Group,
                        CompletedGroups = done,
                        TotalGroups = total,
                        FindingsSoFar = soFar,
                        JustCompleted = result,
                        GroupComplete = groupDone,
                    });
                }
                finally
                {
                    gate.Release();
                }
            }, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        progress?.Report(new ScanProgress
        {
            Current = _scanners[^1].Group,
            CompletedGroups = total,
            TotalGroups = total,
            FindingsSoFar = findingsSoFar,
            IsComplete = true,
        });

        return results;
    }

    /// <summary>Сколько сканеров запускать одновременно — умеренно, чтобы не перегрузить диск (особенно HDD) и WMI.</summary>
    private const int MaxParallelScanners = 4;

    private static async Task<ScanResult> RunSafelyAsync(IScanner scanner, CancellationToken cancellationToken)
    {
        try
        {
            return await scanner.ScanAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ScanResult
            {
                Group = scanner.Group,
                Findings =
                [
                    new Finding
                    {
                        // Уникально на сканер: в группе несколько сканеров (System/Junk — по 4),
                        // иначе два упавших дают одинаковый Id и общий whitelist-ключ.
                        Id = $"scan-error-{scanner.Group}-{scanner.GetType().Name}",
                        Group = scanner.Group,
                        Severity = Severity.Warning,
                        Title = "Не удалось проверить эту группу",
                        Explain = "Во время проверки произошла ошибка, поэтому результаты для этой группы " +
                                  "могут быть неполными. Можно попробовать просканировать ещё раз.",
                        Detail = ex.Message,
                    },
                ],
            };
        }
    }
}
