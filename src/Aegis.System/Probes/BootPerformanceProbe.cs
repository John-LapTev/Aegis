using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Xml.Linq;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Probes;

/// <summary>
/// Читает реальные измерения скорости загрузки Windows из журнала «Microsoft-Windows-Diagnostics-Performance/Operational».
/// Событие 100 — общее время загрузки; 101/102/103 — программы/драйверы/службы, которые её тормозят (с точным временем).
/// Требует прав администратора; на не-Windows или при отключённом журнале честно возвращает пустой результат.
/// </summary>
public sealed class BootPerformanceProbe : IBootPerformanceProbe
{
    private const string Channel = "Microsoft-Windows-Diagnostics-Performance/Operational";
    private const int MaxCulpritEventsScanned = 200; // хватает на много загрузок; дедуп по имени

    public Task<BootPerformance> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(cancellationToken), cancellationToken);

    private static BootPerformance Read(CancellationToken cancellationToken)
    {
        try
        {
            return new BootPerformance
            {
                BootDuration = ReadBootDuration(cancellationToken),
                Culprits = ReadCulprits(cancellationToken),
            };
        }
        catch (Exception)
        {
            return new BootPerformance(); // журнал отключён / нет доступа / не Windows — данных нет
        }
    }

    private static TimeSpan? ReadBootDuration(CancellationToken cancellationToken)
    {
        var query = new EventLogQuery(Channel, PathType.LogName, "*[System[(EventID=100)]]") { ReverseDirection = true };
        using var reader = new EventLogReader(query);

        for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
        {
            using (record)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var data = ReadEventData(record);
                if (data.TryGetValue("BootTime", out var raw)
                    && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) && ms > 0)
                {
                    return TimeSpan.FromMilliseconds(ms); // самое свежее событие 100 (читаем новые→старые)
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<BootCulprit> ReadCulprits(CancellationToken cancellationToken)
    {
        var query = new EventLogQuery(Channel, PathType.LogName, "*[System[(EventID=101 or EventID=102 or EventID=103)]]")
        {
            ReverseDirection = true,
        };
        using var reader = new EventLogReader(query);

        var byName = new Dictionary<string, BootCulprit>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;

        for (var record = reader.ReadEvent(); record is not null && scanned < MaxCulpritEventsScanned; record = reader.ReadEvent())
        {
            using (record)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var data = ReadEventData(record);
                if (!data.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var impactMs = ParseImpact(data);
                if (impactMs <= 0)
                {
                    continue;
                }

                name = name.Trim();
                if (byName.ContainsKey(name))
                {
                    continue; // уже есть более свежее измерение (идём от новых к старым)
                }

                byName[name] = new BootCulprit
                {
                    Name = name,
                    Impact = TimeSpan.FromMilliseconds(impactMs),
                    Kind = record.Id switch
                    {
                        102 => BootCulpritKind.Driver,
                        103 => BootCulpritKind.Service,
                        _ => BootCulpritKind.Application,
                    },
                };
            }
        }

        return byName.Values.OrderByDescending(c => c.Impact).ToList();
    }

    /// <summary>Добавленное к загрузке время: сначала «деградация», иначе общее время элемента.</summary>
    private static long ParseImpact(IReadOnlyDictionary<string, string> data)
    {
        if (data.TryGetValue("DegradationTime", out var degradation)
            && long.TryParse(degradation, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dv) && dv > 0)
        {
            return dv;
        }

        return data.TryGetValue("TotalTime", out var total)
               && long.TryParse(total, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tv)
            ? tv
            : 0;
    }

    /// <summary>Читает пары &lt;Data Name="..."&gt;значение&lt;/Data&gt; из XML события.</summary>
    private static Dictionary<string, string> ReadEventData(EventRecord record)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var document = XDocument.Parse(record.ToXml());

        foreach (var element in document.Descendants().Where(static x => x.Name.LocalName == "Data"))
        {
            var name = element.Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                result[name] = element.Value;
            }
        }

        return result;
    }
}
