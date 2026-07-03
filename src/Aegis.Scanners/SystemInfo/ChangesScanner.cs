using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// «Что изменилось»: сравнивает текущее состояние системы с прошлым снимком и показывает, что ПОЯВИЛОСЬ нового —
/// новые программы, новые элементы автозапуска, новые записи в hosts. Так ловятся тихо установившиеся программы,
/// «попутный» софт и вирусы, которые обычный пользователь не замечает. После сравнения запоминает новый снимок.
/// Находки идут в раздел «Система», в подсекцию «Что изменилось с прошлой проверки».
/// </summary>
public sealed class ChangesScanner : IScanner
{
    private const char Sep = '\u001F';
    private const int MaxPerCategory = 40; // не заваливаем список, если снимок очень старый
    private const string Section = "Что изменилось с прошлой проверки";

    private readonly ISystemSnapshotProbe _probe;
    private readonly ISystemSnapshotStore _store;

    public ChangesScanner(ISystemSnapshotProbe probe, ISystemSnapshotStore store)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(store);
        _probe = probe;
        _store = store;
    }

    public ScanGroup Group => ScanGroup.System;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var current = await _probe.CaptureAsync(cancellationToken).ConfigureAwait(false);
        var previous = _store.LoadLatest();

        var findings = previous is null
            ? [FirstTimeFinding()]
            : BuildDiff(previous, current);

        // Сохраняем новую точку отсчёта, только если снимок непустой — иначе сбой пробника (пустой список)
        // затрёт базу, и следующая проверка ложно покажет ВСЁ как «новое».
        if (current.Programs.Count > 0 || current.HostsEntries.Count > 0)
        {
            _store.Save(current);
        }
        return new ScanResult { Group = ScanGroup.System, Findings = findings };
    }

    private static List<Finding> BuildDiff(SystemSnapshot previous, SystemSnapshot current)
    {
        var findings = new List<Finding>();

        // Новое в автозапуске теперь показывает отдельный AutostartChangesScanner во вкладке «Автозапуск» (правка Ивана 1149).
        var newPrograms = current.Programs.Except(previous.Programs, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var name in newPrograms.Take(MaxPerCategory))
        {
            findings.Add(Make(Severity.Info, "changes-program-" + ScanId.ForPath(name),
                $"Установлена новая программа: {name}",
                $"После прошлой проверки на компьютере появилась программа «{name}». Если ты её ставил — хорошо; если " +
                "нет — возможно, она установилась «в довесок» к другой. Удалить лишнее можно на «Дашборде» → «Удаление программ»."));
        }

        var newHosts = current.HostsEntries.Except(previous.HostsEntries, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var id in newHosts.Take(MaxPerCategory))
        {
            var host = Field(id, 0);
            findings.Add(Make(Severity.Warning, "changes-hosts-" + ScanId.ForPath(id),
                "Новая запись в системном файле hosts",
                $"В системный файл hosts добавилась запись про «{host}». Через этот файл иногда перенаправляют или " +
                "блокируют сайты — так действуют и некоторые вирусы. Если ты это не настраивал — проверь во вкладке «Угрозы»."));
        }

        if (findings.Count == 0)
        {
            findings.Add(Make(Severity.Ok, "changes-none", "С прошлой проверки ничего нового не появилось",
                "Новых программ, элементов автозапуска и правок системного файла hosts с прошлой проверки не обнаружено — " +
                "это хорошо. Тихих установок не было."));
        }

        return findings;
    }

    private static Finding FirstTimeFinding() =>
        Make(Severity.Info, "changes-baseline", "Запомнил текущее состояние компьютера",
            "Aegis запомнил, что сейчас установлено и что в автозапуске. При следующей проверке он покажет, что " +
            "ПОЯВИЛОСЬ нового — так ловятся тихо установившиеся программы и вирусы, которые обычно не замечаешь.");

    private static Finding Make(Severity severity, string id, string title, string explain) => new()
    {
        Id = id,
        Group = ScanGroup.System,
        Severity = severity,
        Title = title,
        Explain = explain,
        Data = new Dictionary<string, string> { ["section"] = Section },
    };

    private static string Field(string id, int index)
    {
        var parts = id.Split(Sep);
        return index < parts.Length ? parts[index] : id;
    }
}
