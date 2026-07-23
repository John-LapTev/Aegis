using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Быстрые показатели здоровья (группа <see cref="ScanGroup.Health"/>): оперативная память, время без
/// перезагрузки, текущая загрузка процессора, обороты вентиляторов. Каждый — плитка с вердиктом простыми
/// словами и шкалой 🟢/🟡/🔴. Только показывает. Недоступные датчики честно помечаются.
/// </summary>
public sealed class SystemVitalsScanner : IScanner
{
    private readonly ISystemVitalsProbe _probe;

    public SystemVitalsScanner(ISystemVitalsProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Health;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var vitals = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);

        var findings = new List<Finding>();
        if (vitals.RamTotalBytes > 0)
        {
            findings.Add(RamFinding(vitals));
        }

        if (vitals.UptimeSeconds > 0)
        {
            findings.Add(UptimeFinding(vitals.UptimeSeconds, vitals.FastStartupEnabled));
        }

        findings.Add(CpuLoadFinding(vitals));

        if (vitals.GpuLoadPercent is not null)
        {
            findings.Add(GpuLoadFinding(vitals));
        }

        findings.Add(FanFinding(vitals.FanRpm));

        return new ScanResult { Group = ScanGroup.Health, Findings = findings };
    }

    private static Finding RamFinding(SystemVitals vitals)
    {
        var percent = vitals.RamUsedPercent;
        var (severity, tail) = percent switch
        {
            >= 92 => (Severity.Danger, "Память почти забита — из-за этого компьютер сильно тормозит и подвисает. " +
                                       "Закрой ненужные программы и вкладки браузера; если это повторяется постоянно — " +
                                       "стоит добавить оперативной памяти."),
            >= 80 => (Severity.Warning, "Памяти занято много — под нагрузкой может начать подтормаживать. Закрой то, " +
                                        "чем не пользуешься (лишние вкладки, программы в фоне)."),
            _ => (Severity.Ok, "Памяти достаточно, всё в порядке."),
        };

        var used = HumanSize.Format(vitals.RamUsedBytes);
        var total = HumanSize.Format(vitals.RamTotalBytes);
        // Объём ОЗУ, округлённый до целых ГБ (маркетинговый размер: 32/16/8…) — подписью под плиткой (правка Ивана 1150).
        var totalGb = (int)Math.Round(vitals.RamTotalBytes / (1024.0 * 1024 * 1024));
        var data = new Dictionary<string, string>
        {
            [FindingDataKeys.HealthIcon] = "memory",
            ["metric"] = $"{percent}%",
            ["metricLabel"] = "занято",
            ["hint"] = "норма: занято примерно до 80%",
        };
        if (totalGb > 0)
        {
            data["model"] = $"{totalGb} ГБ";
        }

        return new Finding
        {
            Id = "health-ram",
            Group = ScanGroup.Health,
            Severity = severity,
            Title = "Оперативная память",
            Detail = $"{used} из {total} занято",
            Explain = $"Оперативная память — это «рабочий стол» компьютера: чем больше свободно, тем шустрее он работает " +
                      $"и тем больше программ можно держать открытыми. Сейчас занято {used} из {total} ({percent}%). {tail} " +
                      "Ориентир: до 80% занято — это норма (даже когда занята половина — нормально: часть держит сама " +
                      "Windows и фоновые программы, даже без открытых окон). Выше 80% стоит присмотреться, а выше 90% " +
                      "компьютер начинает заметно тормозить. Когда открываешь тяжёлые программы или много вкладок, память " +
                      "естественно растёт — это ожидаемо.",
            Data = data,
        };
    }

    private static Finding UptimeFinding(long uptimeSeconds, bool fastStartupEnabled)
    {
        var days = uptimeSeconds / 86400.0;
        var (severity, tail) = days switch
        {
            >= 14 => (Severity.Warning, "Компьютер очень давно не перезагружался. Перезагрузи его — это почистит память, " +
                                        "завершит зависшие программы и почти всегда заметно ускоряет работу."),
            >= 7 => (Severity.Warning, "Компьютер давно работает без перезагрузки. Перезагрузка освежит систему и " +
                                       "уберёт накопившиеся подтормаживания."),
            _ => (Severity.Ok, "Перезагружали недавно — это хорошо, система свежая."),
        };

        // Без этого пояснения цифра выглядит враньём: человек выключает компьютер каждый вечер, а мы пишем
        // «25 дней». Причина — «быстрый запуск»: обычное «Завершение работы» не выключает Windows полностью
        // (ядро сохраняется на диск и восстанавливается), поэтому счётчик продолжает расти (правка Ивана 1347).
        var fastStartupNote = fastStartupEnabled && days >= 3
            ? " Важно: у тебя включён «быстрый запуск» Windows. При нём кнопка «Завершение работы» не выключает " +
              "систему полностью — она сохраняет ядро на диск и утром поднимает его обратно, поэтому этот счётчик " +
              "не обнуляется, даже если ты выключаешь компьютер каждый день. Обнуляет его именно «Перезагрузка» " +
              "в меню Пуск — её и стоит делать раз в неделю."
            : string.Empty;

        return new Finding
        {
            Id = "health-uptime",
            Group = ScanGroup.Health,
            Severity = severity,
            Title = "Время без перезагрузки",
            Detail = HumanUptime(uptimeSeconds),
            Explain = $"Это сколько Windows работает без полной перезагрузки: {HumanUptime(uptimeSeconds)}. {tail}"
                      + fastStartupNote,
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.HealthIcon] = "clock",
                ["metric"] = HumanUptime(uptimeSeconds),
                ["metricLabel"] = "работает",
            },
        };
    }

    private static Finding CpuLoadFinding(SystemVitals vitals)
    {
        if (vitals.CpuLoadPercent is not int load)
        {
            var noData = new Dictionary<string, string>
            {
                [FindingDataKeys.HealthIcon] = "cpu",
                [FindingDataKeys.NoData] = "1",
            };
            AddModel(noData, vitals.CpuName);
            return new Finding
            {
                Id = "health-cpuload",
                Group = ScanGroup.Health,
                Severity = Severity.Info,
                Title = "Загрузка процессора",
                Detail = "не удалось измерить",
                Explain = "Не удалось измерить загрузку процессора — датчик ничего не сообщил. Это не страшно, но и " +
                          "судить по этой плитке сейчас нельзя.",
                Data = noData,
            };
        }

        var (severity, tail) = load switch
        {
            >= 85 => (Severity.Warning, "Процессор сейчас сильно загружен. Если ты ничего тяжёлого не запускал — значит, " +
                                        "что-то работает в фоне (обновление, антивирус, а иногда — скрытый майнер). Загляни " +
                                        "во вкладку «Процессы», чтобы увидеть, что грузит компьютер."),
            >= 50 => (Severity.Ok, "Процессор занят умеренно — это нормально, если открыты программы или браузер."),
            _ => (Severity.Ok, "Процессор почти свободен — компьютер не перегружен."),
        };

        var data = new Dictionary<string, string>
        {
            [FindingDataKeys.HealthIcon] = "cpu",
            ["metric"] = $"{load}%",
            ["metricLabel"] = "загружен",
            ["hint"] = "норма: в покое небольшая; высокая без запущенных программ — что-то грузит в фоне",
        };
        AddModel(data, vitals.CpuName);

        return new Finding
        {
            Id = "health-cpuload",
            Group = ScanGroup.Health,
            Severity = severity,
            Title = "Загрузка процессора",
            Detail = $"{load}% сейчас" + ClockPowerTail(vitals.CpuClockMhz, vitals.CpuPowerWatts),
            Explain = $"Насколько занят «мозг» компьютера прямо сейчас: {load}%. {tail}" + ClockExplain(vitals.CpuClockMhz, vitals.CpuPowerWatts),
            Data = data,
        };
    }

    private static Finding GpuLoadFinding(SystemVitals vitals)
    {
        var load = vitals.GpuLoadPercent ?? 0;
        var severity = load >= 90 ? Severity.Warning : Severity.Ok;
        var memory = vitals is { GpuMemoryUsedMb: int used, GpuMemoryTotalMb: int total } && total > 0
            ? $" Видеопамять: {used} из {total} МБ."
            : string.Empty;
        var power = vitals.GpuPowerWatts is int watts ? $" Потребляет {watts} Вт." : string.Empty;
        var tail = load >= 90
            ? "Видеокарта загружена почти полностью — это нормально в играх и тяжёлых программах; в простое так быть не должно."
            : load >= 30 ? "Видеокарта работает — норма, если открыты игры/видео/графика."
            : "Видеокарта почти свободна.";

        var data = new Dictionary<string, string>
        {
            [FindingDataKeys.HealthIcon] = "gpu",
            ["metric"] = $"{load}%",
            ["metricLabel"] = "загружена",
            ["hint"] = "норма: в покое почти 0; под нагрузкой (игры, видео) — до 100%",
        };
        AddModel(data, vitals.GpuName);

        return new Finding
        {
            Id = "health-gpuload",
            Group = ScanGroup.Health,
            Severity = severity,
            Title = "Загрузка видеокарты",
            Detail = $"{load}% сейчас",
            Explain = $"Насколько занята видеокарта прямо сейчас: {load}%. {tail}{memory}{power}",
            Data = data,
        };
    }

    /// <summary>Добавляет модель железа в Data["model"], если она известна (для подписи под заголовком плитки).</summary>
    private static void AddModel(Dictionary<string, string> data, string? model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            data["model"] = model;
        }
    }

    private static Finding FanFinding(int? rpm)
    {
        // Обороты НЕ прочитаны (rpm=null) — честно «не удалось измерить», даже если известно, что вентилятор
        // ЕСТЬ (fanPresent=true). Иначе получалось ложное «0 об/мин, остановлены», хотя мы просто не смогли
        // прочитать скорость (правка аудита 2026-07-02).
        // Обороты НЕ прочитаны — сюда же попадает случай «датчик есть, но отдаёт ноль»: отличить его от
        // реально остановленного вентилятора невозможно, а зелёное «стоят — всё ок» при неизвестных оборотах
        // вводит в заблуждение (баг с ПК друга Ивана, 2026-07-23). Честно говорим «нет данных».
        if (rpm is null or <= 0)
        {
            return new Finding
            {
                Id = "health-fan",
                Group = ScanGroup.Health,
                Severity = Severity.Info,
                Title = "Вентиляторы",
                Detail = "не удалось измерить",
                Explain = "Обороты вентиляторов узнать не удалось — многие материнские платы и ноутбуки не отдают эти " +
                          "данные программам. Это НЕ значит, что вентиляторы стоят: если бы охлаждение не работало, " +
                          "поднялась бы температура — смотри плитки температуры рядом.",
                Data = new Dictionary<string, string>
                {
                    [FindingDataKeys.HealthIcon] = "fan",
                    [FindingDataKeys.NoData] = "1",
                },
            };
        }

        var speed = rpm.Value;

        return new Finding
        {
            Id = "health-fan",
            Group = ScanGroup.Health,
            Severity = Severity.Ok,
            Title = "Вентиляторы",
            Detail = $"{speed} об/мин",
            Explain = $"Вентиляторы крутятся ({speed} оборотов в минуту) — значит охлаждение работает и отводит тепло.",
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.HealthIcon] = "fan",
                ["metric"] = $"{speed} об/мин",
                ["metricLabel"] = "обороты",
            },
        };
    }

    /// <summary>Хвост к Detail: частота + ватты, если известны («· 4.2 ГГц · 35 Вт»).</summary>
    private static string ClockPowerTail(int? clockMhz, int? watts)
    {
        var parts = new List<string>();
        if (clockMhz is int mhz && mhz > 0)
        {
            parts.Add($"{mhz / 1000.0:0.0} ГГц");
        }

        if (watts is int w && w > 0)
        {
            parts.Add($"{w} Вт");
        }

        return parts.Count > 0 ? " · " + string.Join(" · ", parts) : string.Empty;
    }

    /// <summary>Пояснение про частоту/ватты простыми словами (для «?»).</summary>
    private static string ClockExplain(int? clockMhz, int? watts)
    {
        var text = string.Empty;
        if (clockMhz is int mhz && mhz > 0)
        {
            text += $" Сейчас работает на частоте {mhz / 1000.0:0.0} ГГц (в простое она снижается сама — это экономит энергию, а не поломка).";
        }

        if (watts is int w && w > 0)
        {
            text += $" Потребляет ~{w} Вт.";
        }

        return text;
    }

    /// <summary>Время работы простыми словами: «5 дней», «7 часов», «40 минут».</summary>
    private static string HumanUptime(long seconds)
    {
        if (seconds >= 86400)
        {
            var days = (int)Math.Round(seconds / 86400.0);
            return days + " " + Plural(days, "день", "дня", "дней");
        }

        if (seconds >= 3600)
        {
            var hours = (int)Math.Round(seconds / 3600.0);
            return hours + " " + Plural(hours, "час", "часа", "часов");
        }

        var minutes = Math.Max(1, (int)Math.Round(seconds / 60.0));
        return minutes + " " + Plural(minutes, "минута", "минуты", "минут");
    }

    /// <summary>Русское склонение числительных (1 день / 2 дня / 5 дней).</summary>
    private static string Plural(int n, string one, string few, string many)
    {
        var mod100 = n % 100;
        if (mod100 is >= 11 and <= 14)
        {
            return many;
        }

        return (n % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }
}
