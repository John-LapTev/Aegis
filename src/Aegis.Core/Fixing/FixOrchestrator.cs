using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Core.Fixing;

/// <summary>
/// Реализация безопасной пакетной починки. Порядок строго такой:
/// 1) точка восстановления ПЕРЕД пакетом как «зонтик»; если сама операция бросила исключение —
///    пакет прерывается без правок. Если System Restore просто выключена в Windows
///    (<see cref="BackupRecord.Succeeded"/> = false), пакет всё равно выполняется: обратимость
///    держится на точечных бэкапах каждой правки (экспорт ветки реестра, карантин, Корзина),
///    а несозданная точка не выдаётся за созданную (<see cref="BatchFixResult.RestorePointId"/> = null).
/// 2) применяем каждое исправление (каждое само делает свой точечный бэкап по контракту);
/// 3) одна упавшая правка не отменяет остальные — каждая обратима и независима.
/// </summary>
public sealed class FixOrchestrator : IFixOrchestrator
{
    private readonly IRestorePointService _restorePoints;

    public FixOrchestrator(IRestorePointService restorePoints)
    {
        ArgumentNullException.ThrowIfNull(restorePoints);
        _restorePoints = restorePoints;
    }

    public async Task<BatchFixResult> ApplyAsync(
        IReadOnlyList<IFix> fixes,
        string batchDescription,
        IProgress<FixProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixes);

        if (fixes.Count == 0)
        {
            return new BatchFixResult { Outcomes = [], RequiresReboot = false };
        }

        // 1) «Зонтичную» точку восстановления создаём ТОЛЬКО если хоть одна правка её требует (системные
        // изменения). Для безопасных удалений (мусор/кэш → Корзина) пропускаем: обратимость даёт Корзина,
        // а создание точки (VSS) медленное и может зависать на проблемной системе — зря тормозило бы чистку.
        BackupRecord? restorePoint = null;
        if (fixes.Any(static f => f.RequiresSystemRestorePoint))
        {
            try
            {
                restorePoint = await _restorePoints
                    .CreateRestorePointAsync(batchDescription, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new BatchFixResult
                {
                    Outcomes = [],
                    RequiresReboot = false,
                    Aborted = true,
                    Message = "Не удалось создать точку восстановления, поэтому изменения не вносились — " +
                              $"так безопаснее. Подробности: {ex.Message}",
                };
            }
        }

        // 2) Применяем исправления по очереди.
        var outcomes = new List<FixOutcome>(fixes.Count);
        var requiresReboot = false;

        for (var i = 0; i < fixes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fix = fixes[i];
            progress?.Report(new FixProgress
            {
                FindingId = fix.FindingId,
                Completed = i,
                Total = fixes.Count,
            });

            var outcome = await ApplySafelyAsync(fix, cancellationToken).ConfigureAwait(false);
            outcomes.Add(outcome);
            requiresReboot |= outcome.RequiresReboot;
        }

        progress?.Report(new FixProgress
        {
            FindingId = fixes[^1].FindingId,
            Completed = fixes.Count,
            Total = fixes.Count,
        });

        return new BatchFixResult
        {
            Outcomes = outcomes,
            RequiresReboot = requiresReboot,
            // Только если точка действительно создана. Иначе откат — через точечные бэкапы в разделе «Бэкапы».
            RestorePointId = restorePoint is { Succeeded: true } ? restorePoint.Id : null,
        };
    }

    private static async Task<FixOutcome> ApplySafelyAsync(IFix fix, CancellationToken cancellationToken)
    {
        try
        {
            return await fix.ApplyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FixOutcome.Failed($"Не удалось применить исправление: {ex.Message}");
        }
    }
}
