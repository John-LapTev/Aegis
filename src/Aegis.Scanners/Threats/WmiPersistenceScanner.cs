using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Threats;

/// <summary>
/// Постоянные подписки WMI (группа <see cref="ScanGroup.Threats"/>) — скрытный механизм автозапуска, любимый
/// майнерами/вирусами и почти не используемый обычными программами. С явными признаками вредоноса в команде —
/// «Опасно»; иначе — «Внимание» (редкий механизм, стоит присмотреться). Известные легитимные — в белом списке.
/// </summary>
public sealed class WmiPersistenceScanner : IScanner
{
    // Известные легитимные подписки (белый список — чтобы не пугать). Расширяется по результатам проверки на Win11.
    private static readonly HashSet<string> KnownGoodConsumers = new(StringComparer.OrdinalIgnoreCase)
    {
        "SCM Event Provider",
    };

    // Сильные признаки вредоноса в команде/скрипте подписки (редко встречаются у легитимных).
    private static readonly string[] MaliciousMarkers =
    [
        "-enc", "-ec ", "encodedcommand", "frombase64", "downloadstring", "downloadfile",
        "iex", "invoke-expression", "-nop", "-w hidden", "-windowstyle hidden", "mshta",
        "\\temp\\", "%temp%", "\\appdata\\", "bitsadmin", "certutil",
    ];

    private readonly IWmiPersistenceProbe _probe;

    public WmiPersistenceScanner(IWmiPersistenceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Threats;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var items = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = items
            .Where(item => !KnownGoodConsumers.Contains(item.Name))
            .Select(CreateFinding)
            .ToList();

        return new ScanResult { Group = ScanGroup.Threats, Findings = findings };
    }

    /// <summary>
    /// Есть ли маркер вредоноса в команде. Короткий буквенный маркер «iex» (Invoke-Expression) — только как
    /// отдельное слово, иначе легитимный <c>iexplore.exe</c> давал бы ложное «Опасно» (правка аудита).
    /// </summary>
    private static bool ContainsMarker(string action, string marker)
    {
        if (marker != "iex")
        {
            return action.Contains(marker, StringComparison.OrdinalIgnoreCase);
        }

        var i = action.IndexOf("iex", StringComparison.OrdinalIgnoreCase);
        while (i >= 0)
        {
            var boundedLeft = i == 0 || !char.IsLetter(action[i - 1]);
            var afterIndex = i + 3;
            var boundedRight = afterIndex >= action.Length || !char.IsLetter(action[afterIndex]);
            if (boundedLeft && boundedRight)
            {
                return true;
            }

            i = action.IndexOf("iex", i + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static Finding CreateFinding(WmiPersistence item)
    {
        var malicious = MaliciousMarkers.Any(marker => ContainsMarker(item.Action, marker));

        return new Finding
        {
            Id = $"wmi-persistence-{item.Name}",
            Group = ScanGroup.Threats,
            Severity = malicious ? Severity.Danger : Severity.Warning,
            Title = malicious
                ? $"Скрытый автозапуск через WMI: {item.Name}"
                : $"Подписка WMI (автозапуск): {item.Name}",
            Detail = Truncate(item.Action, 200),
            Explain = malicious
                ? "Найдена скрытая подписка WMI, которая при системном событии запускает подозрительную " +
                  $"{item.Kind} (есть признаки вредоноса). Это любимый приём скрытых майнеров и вирусов: " +
                  "переживает перезагрузку и не виден в обычном списке автозапуска. Настоятельно проверь " +
                  "компьютер антивирусом (полная проверка Защитником Windows)."
                : "Найдена подписка WMI — редкий механизм автозапуска (при событии выполняется " +
                  $"{item.Kind}). Обычные программы им почти не пользуются. Если это поставил ты или твоя " +
                  "программа (антивирус, система мониторинга на работе) — всё в порядке. Если не знаешь, " +
                  "откуда это, — стоит присмотреться и на всякий случай проверить антивирусом.",
        };
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
