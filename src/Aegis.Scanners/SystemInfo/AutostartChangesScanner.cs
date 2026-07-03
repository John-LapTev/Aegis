using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// «Новое в автозапуске»: показывает элементы автозапуска, ПОЯВИВШИЕСЯ с прошлой проверки. Раньше это жило в «Системе»
/// (сканер «Что изменилось»), но всё, что связано с автозапуском, логичнее видеть во вкладке «Автозапуск» (запрос
/// Ивана 1149) — поэтому это отдельный сканер группы <see cref="ScanGroup.Autostart"/> со СВОИМ файлом-снимком
/// (независимо от «Что изменилось» по программам/hosts). После сравнения запоминает новый снимок.
/// </summary>
public sealed class AutostartChangesScanner : IScanner
{
    private const char Sep = '\u001F';
    private const int MaxItems = 40;
    private const string Section = "Новое в автозапуске";

    private readonly ISystemSnapshotProbe _probe;
    private readonly ISystemSnapshotStore _store;

    public AutostartChangesScanner(ISystemSnapshotProbe probe, ISystemSnapshotStore store)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(store);
        _probe = probe;
        _store = store;
    }

    public ScanGroup Group => ScanGroup.Autostart;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var current = await _probe.CaptureAsync(cancellationToken).ConfigureAwait(false);
        var previous = _store.LoadLatest();

        var findings = previous is null
            ? [FirstTimeFinding()]
            : BuildDiff(previous, current);

        // Сохраняем базу только если автозапуск непустой — иначе сбой пробника затрёт базу и следующая проверка
        // ложно покажет все записи как «новые».
        if (current.Autostart.Count > 0)
        {
            _store.Save(current);
        }

        return new ScanResult { Group = ScanGroup.Autostart, Findings = findings };
    }

    private static List<Finding> BuildDiff(SystemSnapshot previous, SystemSnapshot current)
    {
        var findings = new List<Finding>();

        var added = current.Autostart.Except(previous.Autostart, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var id in added.Take(MaxItems))
        {
            var name = Field(id, 1);
            findings.Add(Make(Severity.Warning, "autostart-new-" + ScanId.ForPath(id),
                $"Новое в автозапуске: {name}",
                $"После прошлой проверки в автозапуск добавилась программа «{name}» — теперь она стартует вместе с " +
                "Windows. Если ты её ставил осознанно — всё в порядке; если нет — это может быть «попутный» софт или " +
                "вирус. Ниже в списке автозапуска её можно отключить."));
        }

        if (findings.Count == 0)
        {
            findings.Add(Make(Severity.Ok, "autostart-new-none", "Новых программ в автозапуске не появилось",
                "С прошлой проверки в автозапуск ничего нового не добавилось — это хорошо. Тихих установок не было."));
        }

        return findings;
    }

    private static Finding FirstTimeFinding() =>
        Make(Severity.Info, "autostart-new-baseline", "Запомнил, что сейчас в автозапуске",
            "Aegis запомнил текущий список автозапуска. При следующей проверке он покажет, что ПОЯВИЛОСЬ нового — " +
            "так ловятся тихо прописавшиеся в автозапуск программы, которые обычно не замечаешь.");

    private static Finding Make(Severity severity, string id, string title, string explain) => new()
    {
        Id = id,
        Group = ScanGroup.Autostart,
        Severity = severity,
        Title = title,
        Explain = explain,
        Data = new Dictionary<string, string> { ["section"] = Section, ["info"] = "1" },
    };

    private static string Field(string id, int index)
    {
        var parts = id.Split(Sep);
        return index < parts.Length ? parts[index] : id;
    }
}
