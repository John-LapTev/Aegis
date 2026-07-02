using System.Globalization;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Сканер здоровья дисков (SMART), группа <see cref="ScanGroup.Health"/>. В отличие от сканеров
/// проблем, показывает статус КАЖДОГО диска (шкала 🟢/🟡/🔴), чтобы человек видел зелёную/жёлтую/красную
/// зону и понятный вывод «норма / стоит присмотреть / опасно» (требование UX — аудитория не техническая).
/// </summary>
public sealed class DiskHealthScanner : IScanner
{
    private readonly IDiskHealthProbe _probe;

    public DiskHealthScanner(IDiskHealthProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Health;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var drives = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);

        var findings = drives
            .Select(CreateFinding)
            .ToList();

        return new ScanResult { Group = ScanGroup.Health, Findings = findings };
    }

    private static Finding CreateFinding(SmartDriveHealth drive)
    {
        var (severity, verdict, explain) = Describe(drive.Level);

        // Вердикт (всё хорошо/присмотреть/опасно) не дублируем в заголовок — его показывает цветной статус-бейдж.
        _ = verdict;

        // Ресурс SSD: сильный износ — самостоятельный сигнал здоровья, даже если общий SMART-уровень «норма».
        if (drive.PercentLifeUsed is int used)
        {
            severity = MoreSevere(severity, used >= 90 ? Severity.Danger : used >= 70 ? Severity.Warning : Severity.Ok);
            explain += WearText(used);
        }

        // Температура диска: SSD/HDD от жары тормозят и быстрее изнашиваются.
        if (drive.TemperatureCelsius is int temp)
        {
            severity = MoreSevere(severity, temp >= 70 ? Severity.Danger : temp >= 60 ? Severity.Warning : Severity.Ok);
            explain += DiskTempText(temp);
        }

        // Правый-верхний угол плитки: либо блок «% заполнения» (свой цвет), либо серый «RAW» (нечитаемый диск).
        var data = new Dictionary<string, string>();
        if (drive.FillPercent is int fill)
        {
            data["fillPercent"] = fill.ToString(CultureInfo.InvariantCulture);
            data["fillSeverity"] = FillSeverity(fill);
        }

        // Буква диска (C, D…) — подписью в левом-верхнем углу иконки. Нет буквы (RAW/без буквы) → надпись «RAW».
        if (drive.Letter is char letter)
        {
            data["letter"] = letter.ToString();
        }

        if (drive.FilesystemUnreadable)
        {
            data["raw"] = "1"; // нет буквы → в углу иконки покажем «RAW» (простой надписью, без «?»/бейджа)
        }

        // Нечитаемый формат поясняем в «?» (Explain), а не длинным текстом в подписи плитки.
        var note = drive.FilesystemUnreadable
            ? " У этого диска Windows не распознаёт формат (RAW): он отформатирован не под Windows или пустой. " +
              "Поэтому заполнение показать нельзя — данных о нём у системы нет. Само «здоровье» диска при этом в норме."
            : string.Empty;

        return new Finding
        {
            Id = $"disk-health-{drive.Name}",
            Group = ScanGroup.Health,
            Severity = severity,
            Title = $"Диск {drive.Name}",
            Detail = BuildMetrics(drive),
            Explain = explain + note,
            Data = data.Count > 0 ? data : null,
        };
    }

    /// <summary>Более тяжёлая из двух оценок (по порядку Ok‹Info‹Warning‹Danger).</summary>
    private static Severity MoreSevere(Severity a, Severity b) => (Severity)Math.Max((int)a, (int)b);

    /// <summary>Понятный вывод по температуре диска.</summary>
    private static string DiskTempText(int temp)
    {
        var verdict = temp switch
        {
            >= 70 => "это горячо для диска — он может сбрасывать скорость и быстрее изнашиваться; улучши обдув корпуса",
            >= 60 => "тепловато — под нагрузкой стоит последить, не помешает продуть корпус от пыли",
            _ => "нормальная температура",
        };

        return $" Температура диска — {temp} °C: {verdict}.";
    }

    /// <summary>Понятный вывод по ресурсу SSD (сколько «жизни» осталось на запись данных).</summary>
    private static string WearText(int used)
    {
        var left = Math.Clamp(100 - used, 0, 100);
        var verdict = used switch
        {
            >= 90 => "ресурс почти исчерпан — заранее скопируй важные файлы и присматривай за диском, со временем стоит заменить",
            >= 70 => "ресурс заметно израсходован, но диск ещё рабочий — просто присматривай за ним",
            _ => "запас большой, всё в порядке",
        };

        return $" Ресурс SSD: израсходовано примерно {used}% (осталось ~{left}%) — {verdict}. " +
               "Это про то, сколько данных на диск ещё можно записать за его срок службы (касается только SSD).";
    }

    /// <summary>Цвет блока заполнения: &gt;90% — опасно, &gt;80% — внимание, иначе — норма.</summary>
    private static string FillSeverity(int fillPercent) =>
        fillPercent >= 90 ? "danger" : fillPercent >= 80 ? "warning" : "ok";

    private static (Severity Severity, string Verdict, string Explain) Describe(SmartHealthLevel level) => level switch
    {
        SmartHealthLevel.Good => (
            Severity.Ok,
            "всё хорошо",
            "Диск чувствует себя хорошо — это норма, волноваться не нужно."),
        SmartHealthLevel.Warning => (
            Severity.Warning,
            "стоит присмотреть",
            "У диска появились тревожные признаки. Пока работает, но на всякий случай заранее скопируй важные " +
            "файлы (фото, документы) на другой диск или флешку и присматривай за ним."),
        SmartHealthLevel.Critical => (
            Severity.Danger,
            "опасно",
            "Диск в плохом состоянии и может скоро выйти из строя — есть риск потерять данные. Срочно скопируй " +
            "важные файлы на другой диск или флешку, а этот диск стоит заменить."),
        _ => (
            Severity.Info,
            "не удалось оценить",
            "Не получилось проверить здоровье этого диска."),
    };

    private static string? BuildMetrics(SmartDriveHealth drive)
    {
        var parts = new List<string>(4);

        if (!string.IsNullOrWhiteSpace(drive.Model))
        {
            parts.Add(drive.Model);
        }

        if (drive.PercentLifeUsed is int used)
        {
            parts.Add($"износ {used}%");
        }

        if (drive.TemperatureCelsius is int temp)
        {
            parts.Add($"{temp} °C");
        }

        // Переназначенные сектора — это жаргон; на плитку не выносим (о деградации честно скажет вердикт «стоит присмотреть»).
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }
}
