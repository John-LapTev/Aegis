using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// «Раннее предупреждение по трендам»: сравнивает текущие SMART-показатели дисков с накопленной историей и
/// предупреждает ЗАРАНЕЕ, пока диск ещё работает — растёт число переназначенных секторов («диск начал сыпаться»),
/// быстро увеличивается износ SSD, диск греется сильнее прежнего, а также показывает «занято было X% — стало Y%».
/// Смысл: обычный SMART-сканер видит только «сейчас», а болезнь диска — это ДИНАМИКА. Находки идут в раздел
/// «Система», подсекцию «Динамика дисков во времени». После сравнения дописывает текущий снимок в историю.
/// </summary>
public sealed class TrendsScanner : IScanner
{
    private const string Section = "Динамика дисков во времени";
    private const int WearJumpPoints = 5;       // рост износа SSD, при котором отдельно предупреждаем о темпе
    private const int TempRisePoints = 8;        // насколько диск стал горячее прежнего, чтобы бить тревогу
    private const int TempWarnFloor = 55;        // ниже этого «нагрев» несущественен, даже если вырос
    private const int FillChangePoints = 3;      // на сколько должно измениться заполнение, чтобы это показать

    private readonly IDiskHealthProbe _probe;
    private readonly IHealthTrendStore _store;

    public TrendsScanner(IDiskHealthProbe probe, IHealthTrendStore store)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(store);
        _probe = probe;
        _store = store;
    }

    public ScanGroup Group => ScanGroup.System;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var drives = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var history = _store.LoadHistory();

        var current = new HealthTrendSnapshot
        {
            CapturedAt = DateTimeOffset.Now,
            Disks = drives.Select(d => new DiskTrendPoint
            {
                Name = d.Name,
                PercentLifeUsed = d.PercentLifeUsed,
                ReallocatedSectorCount = d.ReallocatedSectorCount,
                TemperatureCelsius = d.TemperatureCelsius,
                FillPercent = d.FillPercent,
            }).ToList(),
        };

        var findings = history.Count == 0
            ? [BaselineFinding()]
            : BuildTrends(history, current);

        if (current.Disks.Count > 0)
        {
            _store.Append(current); // копим историю только когда есть что копить
        }

        return new ScanResult { Group = ScanGroup.System, Findings = findings };
    }

    private static List<Finding> BuildTrends(IReadOnlyList<HealthTrendSnapshot> history, HealthTrendSnapshot current)
    {
        var findings = new List<Finding>();

        foreach (var disk in current.Disks)
        {
            // Все прошлые точки этого диска, от старых к новым.
            var past = history.SelectMany(s => s.Disks).Where(p => p.Name == disk.Name).ToList();

            AddReallocatedTrend(findings, disk, past);
            AddWearTrend(findings, disk, past);
            AddTemperatureTrend(findings, disk, past);
            AddFillTrend(findings, disk, past);
        }

        if (findings.Count == 0)
        {
            findings.Add(Make(Severity.Ok, "trends-none", "В динамике дисков всё спокойно",
                "Aegis сравнил показатели дисков с прошлыми проверками — тревожных изменений нет: сектора не сыплются, " +
                "износ и температура не растут. Это хорошо. Продолжаю следить дальше."));
        }

        return findings;
    }

    /// <summary>Переназначенные сектора — главный ранний признак «диск сыплется».</summary>
    private static void AddReallocatedTrend(List<Finding> findings, DiskTrendPoint disk, List<DiskTrendPoint> past)
    {
        if (disk.ReallocatedSectorCount is not int current || current <= 0)
        {
            return; // 0 секторов — норма, ничего не пишем
        }

        var baseline = past.Select(p => p.ReallocatedSectorCount).FirstOrDefault(v => v is not null);

        if (baseline is int was && current > was)
        {
            findings.Add(Make(Severity.Danger, $"trends-realloc-{disk.Name}",
                $"Диск {disk.Name} начал сыпаться — и это усиливается",
                $"На диске {disk.Name} растёт число «переназначенных секторов» — это участки, которые диск признал " +
                $"негодными и заменил запасными. Было {was}, стало {current}. Рост — тревожный признак: диск может " +
                "скоро выйти из строя, есть риск потерять данные. СРОЧНО скопируй важные файлы (фото, документы) на " +
                "другой диск или флешку, а этот диск стоит заменить."));
        }
        else
        {
            findings.Add(Make(Severity.Warning, $"trends-realloc-{disk.Name}",
                $"На диске {disk.Name} есть повреждённые участки",
                $"На диске {disk.Name} есть «переназначенные сектора» ({current} шт.) — участки, которые диск признал " +
                "негодными и заменил запасными. Пока их число не растёт, диск работает, но это повод присмотреться: " +
                "заранее скопируй важные файлы на другой носитель. Aegis продолжит следить, не начнётся ли рост."));
        }
    }

    /// <summary>Быстрый рост износа SSD — отдельный сигнал (обычный сканер показывает только «сейчас»).</summary>
    private static void AddWearTrend(List<Finding> findings, DiskTrendPoint disk, List<DiskTrendPoint> past)
    {
        if (disk.PercentLifeUsed is not int current)
        {
            return;
        }

        var baseline = past.Select(p => p.PercentLifeUsed).FirstOrDefault(v => v is not null);
        if (baseline is not int was || current - was < WearJumpPoints)
        {
            return; // износ не растёт заметно — не шумим (абсолютный уровень покажет сканер здоровья)
        }

        var severity = current >= 70 ? Severity.Warning : Severity.Info;
        findings.Add(Make(severity, $"trends-wear-{disk.Name}",
            $"Ресурс SSD {disk.Name} расходуется быстро",
            $"У SSD {disk.Name} износ вырос с {was}% до {current}% за время наблюдения — расходуется заметно быстрее " +
            "обычного. Само по себе это не поломка, но если так пойдёт и дальше, диск состарится раньше. Причина часто — " +
            "программа, которая постоянно много пишет на диск. Скопировать важное заранее в любом случае не помешает."));
    }

    /// <summary>Диск греется сильнее, чем раньше — обдув/пыль.</summary>
    private static void AddTemperatureTrend(List<Finding> findings, DiskTrendPoint disk, List<DiskTrendPoint> past)
    {
        if (disk.TemperatureCelsius is not int current || current < TempWarnFloor)
        {
            return;
        }

        var baseline = past.Select(p => p.TemperatureCelsius).FirstOrDefault(v => v is not null);
        if (baseline is not int was || current - was < TempRisePoints)
        {
            return;
        }

        findings.Add(Make(Severity.Warning, $"trends-temp-{disk.Name}",
            $"Диск {disk.Name} стал горячее прежнего",
            $"Температура диска {disk.Name} выросла: было около {was} °C, стало {current} °C. От перегрева диск сбрасывает " +
            "скорость и быстрее изнашивается. Обычно помогает продуть корпус от пыли и проверить, крутятся ли вентиляторы."));
    }

    /// <summary>«Было занято X% — стало Y%»: динамика места на диске (что просил показывать).</summary>
    private static void AddFillTrend(List<Finding> findings, DiskTrendPoint disk, List<DiskTrendPoint> past)
    {
        if (disk.FillPercent is not int current)
        {
            return;
        }

        // Сравниваем с ПОСЛЕДНИМ известным заполнением (изменение с прошлой проверки), а не с самым старым.
        var previous = past.Select(p => p.FillPercent).LastOrDefault(v => v is not null);
        if (previous is not int was || Math.Abs(current - was) < FillChangePoints)
        {
            return;
        }

        if (current < was)
        {
            var freed = was - current;
            findings.Add(Make(Severity.Ok, $"trends-fill-{disk.Name}",
                $"На диске {disk.Name} стало свободнее",
                $"На диске {disk.Name} освободилось место: было занято {was}%, стало {current}% (–{freed}%). " +
                "Хорошо — свободное место помогает компьютеру работать бодрее."));
        }
        else
        {
            var added = current - was;
            var tail = current >= 90
                ? " Диск почти полон — это уже мешает Windows нормально работать, стоит освободить место (загляни во вкладку «Мусор»)."
                : string.Empty;
            findings.Add(Make(Severity.Info, $"trends-fill-{disk.Name}",
                $"Диск {disk.Name} заполняется",
                $"На диске {disk.Name} стало меньше свободного места: было занято {was}%, стало {current}% (+{added}%)." + tail));
        }
    }

    private static Finding BaselineFinding() =>
        Make(Severity.Info, "trends-baseline", "Начал следить за состоянием дисков во времени",
            "Aegis запомнил текущие показатели дисков (износ, температуру, повреждённые участки, заполнение). При " +
            "следующих проверках он будет сравнивать их и заранее предупредит, если диск начнёт «сыпаться» или " +
            "перегреваться — задолго до того, как это станет заметно по работе компьютера.");

    private static Finding Make(Severity severity, string id, string title, string explain) => new()
    {
        Id = id,
        Group = ScanGroup.System,
        Severity = severity,
        Title = title,
        Explain = explain,
        Data = new Dictionary<string, string> { [FindingDataKeys.Section] = Section },
    };
}
