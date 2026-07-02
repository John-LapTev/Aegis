using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Восстановление системных файлов Windows: сначала DISM (восстанавливает образ),
/// затем SFC (проверяет и чинит защищённые файлы). Штатные средства Microsoft, безопасно.
/// </summary>
public sealed class SfcDismRepairFix : IFix, IProgressReportingFix
{
    public SfcDismRepairFix(string findingId) => FindingId = findingId;

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.System;

    public IProgress<double>? Progress { get; set; }

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Два этапа: DISM — первая половина кольца (0..0.5), SFC — вторая (0.5..1.0).
            var dism = await ProcessRunner.RunAsync(
                ProcessRunner.System("Dism.exe"), "/online /cleanup-image /restorehealth", cancellationToken,
                Scaled(0.0, 0.5)).ConfigureAwait(false);

            var sfc = await ProcessRunner.RunAsync(
                ProcessRunner.System("sfc.exe"), "/scannow", cancellationToken, Scaled(0.5, 1.0)).ConfigureAwait(false);

            if (dism < 0 && sfc < 0)
            {
                return FixOutcome.Failed("Не удалось запустить проверку системных файлов.");
            }

            MaintenanceHistory.MarkRun(MaintenanceHistory.SfcDismKey); // запомнить «запускали» → покажем дату
            return FixOutcome.OkWithoutBackup();
        }
        catch (OperationCanceledException)
        {
            throw; // отмена пользователем — пусть дойдёт как «Операция отменена», а не «ошибка»
        }
        catch (Exception ex)
        {
            return FixOutcome.Failed("Не удалось починить системные файлы: " + ex.Message);
        }
    }

    /// <summary>Прогресс одного этапа (0..1) → в общий диапазон [from..to] кольца. Null, если прогресс не нужен.</summary>
    private IProgress<double>? Scaled(double from, double to) =>
        Progress is null ? null : new Progress<double>(p => Progress.Report(from + (Math.Clamp(p, 0, 1) * (to - from))));
}
