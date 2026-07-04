using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Privacy;

/// <summary>
/// Сканер приватности и «лишнего» Windows (группа <see cref="ScanGroup.Settings"/>). Находит включённые
/// слежку/телеметрию/рекламу и фоновый хлам, которые зря нагружают систему, и предлагает выключить.
/// Всё помечается как «не опасно, можно отключить» (severity Info) — это выбор, а не проблема (UX-правило).
/// Каждое выключение обратимо (предыдущее состояние сохраняется ПЕРЕД изменением).
/// </summary>
public sealed class PrivacyDebloatScanner : IScanner
{
    private readonly IPrivacyProbe _probe;

    public PrivacyDebloatScanner(IPrivacyProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        // Телеметрию показываем со статусом. ВАЖНО: null = уровень НЕ прочитан (нет прав/ключа) — это НЕ
        // «всё хорошо». Не выдаём непрочитанное за минимум, иначе соврём про приватность (правка аудита).
        if (snapshot.TelemetryLevel is null)
        {
            findings.Add(new Finding
            {
                Id = "privacy-telemetry-unknown",
                Group = ScanGroup.Settings,
                Severity = Severity.Info,
                Title = "Уровень телеметрии проверить не удалось",
                Detail = "Не смогли прочитать настройку",
                Explain = "Не получилось узнать, сколько данных Windows отправляет в Microsoft (настройка недоступна " +
                          "для чтения). Это не значит, что всё хорошо или плохо — просто проверить не вышло.",
            });
        }
        else if (snapshot.TelemetryLevel >= 2)
        {
            findings.Add(Privacy(
                "privacy-telemetry-full",
                "Windows отправляет о тебе расширенные данные",
                "Включена телеметрия по максимуму",
                "Windows постоянно отправляет в Microsoft подробные данные о работе компьютера. Это не опасно, " +
                "но многим не нужно и слегка нагружает систему. Можно уменьшить до минимума."));
        }
        else
        {
            findings.Add(AlreadyDone(
                "privacy-telemetry-ok",
                "Телеметрия на минимальном уровне",
                "Отправка данных ограничена — менять не нужно",
                "Windows отправляет в Microsoft только минимум данных о работе системы (или отправка " +
                "ограничена). Это хорошо для приватности — делать ничего не нужно."));
        }

        // Показываем ВСЕ пункты (правка 729/731): включённые — «можно отключить», уже выключенные — зелёным «уже
        // отключено» (видно полную картину и что уже сделано). Только не в «Мусоре» — там удалённого уже нет.
        findings.AddRange(snapshot.Toggles.Select(CreateToggleFinding));
        findings.AddRange(snapshot.DebloatItems.Select(CreateDebloatFinding));

        return new ScanResult { Group = ScanGroup.Settings, Findings = findings };
    }

    // Уже в нужном состоянии (зелёное «Исправлено», без кнопки) — единый вид для телеметрии/тумблеров/хлама.
    private static Finding AlreadyDone(string id, string title, string detail, string explain) => new()
    {
        Id = id,
        Group = ScanGroup.Settings,
        Severity = Severity.Ok,
        Title = title,
        Detail = detail,
        Explain = explain,
        Data = new Dictionary<string, string> { ["done"] = "1" },
    };

    private static Finding Privacy(string id, string title, string detail, string explain) => new()
    {
        Id = id,
        Group = ScanGroup.Settings,
        Severity = Severity.Info,
        Title = title,
        Detail = detail,
        Explain = explain,
    };

    private static Finding CreateToggleFinding(PrivacyToggle toggle) => toggle.Enabled
        ? new Finding
        {
            Id = toggle.Id,
            Group = ScanGroup.Settings,
            Severity = Severity.Info,
            Title = toggle.Title,
            Detail = toggle.Detail,
            Explain = toggle.Explain,
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.RegistryToggle,
                ["hive"] = toggle.Hive,
                ["subkey"] = toggle.SubKey,
                ["name"] = toggle.ValueName,
                ["value"] = toggle.DisableValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
        }
        : AlreadyDone(toggle.Id, toggle.Title, "Уже отключено",
            "Это уже отключено — делать ничего не нужно. Показано, чтобы было видно, что уже сделано.");

    private static Finding CreateDebloatFinding(DebloatItem item)
    {
        var id = item.ServiceName is not null ? $"debloat-{item.ServiceName}" : $"debloat-{item.Name}";
        if (!item.Enabled)
        {
            return AlreadyDone(id, $"Лишнее: {item.Name}", "Уже отключено",
                "Это уже отключено — делать ничего не нужно. Показано, чтобы было видно, что уже сделано.");
        }

        return new Finding
        {
            Id = id,
            Group = ScanGroup.Settings,
            Severity = Severity.Info,
            Title = $"Лишнее: {item.Name}",
            Detail = item.Category,
            Explain = $"Это ({item.Category}) обычно не нужно и работает в фоне, понемногу нагружая систему. " +
                      "Можно безопасно отключить — на работу нужных программ не повлияет. При желании вернём обратно.",
            Data = item.ServiceName is not null
                ? new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.ServiceDisable, ["service"] = item.ServiceName }
                : item.TaskName is not null
                    ? new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.TaskDisable, ["task"] = item.TaskName }
                    : null,
        };
    }
}
