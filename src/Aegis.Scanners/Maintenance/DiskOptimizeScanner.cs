using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Maintenance;

/// <summary>
/// Обслуживание дисков (группа <see cref="ScanGroup.System"/>): Windows должна регулярно наводить порядок
/// на дисках сама — для твердотельных это команда TRIM (без неё запись со временем замедляется), для
/// обычных — дефрагментация. Расписание часто оказывается выключенным, и тогда об этом никто не узнаёт.
/// </summary>
public sealed class DiskOptimizeScanner : IScanner
{
    /// <summary>Windows делает это раз в неделю; два месяца тишины — уже повод показать кнопку.</summary>
    internal const int StaleDays = 60;

    private readonly IDiskOptimizeProbe _probe;

    public DiskOptimizeScanner(IDiskOptimizeProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.System;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var state = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (!NeedsAttention(state))
        {
            return new ScanResult { Group = ScanGroup.System, Findings = [] };
        }

        var detail = !state.ScheduleEnabled
            ? "автоматическое обслуживание выключено"
            : $"последний раз — {state.DaysSinceLastRun} дн. назад";

        var whatItDoes = state.HasSolidStateDrive
            ? "У тебя твердотельный диск (SSD). Ему нужна не дефрагментация, а команда TRIM: она сообщает диску, " +
              "какие ячейки уже не нужны, и без неё запись со временем становится медленнее. Windows делает это " +
              "сама раз в неделю — если расписание не работает, стоит запустить вручную."
            : "Windows раз в неделю наводит порядок на дисках: собирает разбросанные куски файлов вместе, чтобы " +
              "они читались быстрее. Если этого давно не было, компьютер может работать медленнее.";

        var finding = new Finding
        {
            Id = "maintenance-disk-optimize",
            Group = ScanGroup.System,
            Severity = Severity.Info,
            Title = "Диски давно не обслуживались",
            Detail = detail,
            Explain = whatItDoes + " Кнопка запустит штатное средство Windows — оно ничего не удаляет и не " +
                      "меняет файлы, только наводит порядок в их расположении. Может занять от нескольких минут " +
                      "до часа; в это время компьютером можно пользоваться, но он будет менее отзывчивым.",
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.DiskOptimize,
                [FindingDataKeys.Section] = "Обслуживание",
            },
        };

        return new ScanResult { Group = ScanGroup.System, Findings = [finding] };
    }

    /// <summary>Показывать ли находку: выключенное расписание либо слишком давняя последняя оптимизация.</summary>
    internal static bool NeedsAttention(DiskOptimizeState state) =>
        !state.ScheduleEnabled || state.DaysSinceLastRun >= StaleDays;
}
