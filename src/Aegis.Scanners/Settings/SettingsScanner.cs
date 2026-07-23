using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Settings;

/// <summary>
/// Сканер системных настроек (группа <see cref="ScanGroup.Settings"/>). Проверяет ключевые
/// параметры безопасности/обслуживания и сообщает о небезопасных отклонениях. Включение защиты —
/// обратимая операция (предыдущее состояние сохраняется ПЕРЕД изменением).
/// </summary>
public sealed class SettingsScanner : IScanner
{
    private readonly ISettingsProbe _probe;

    public SettingsScanner(ISettingsProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        if (!settings.FirewallEnabled)
        {
            findings.Add(FirewallFinding(settings.DisabledFirewallProfiles));
        }

        if (!settings.UacEnabled)
        {
            findings.Add(new Finding
            {
                Id = "settings-uac-off",
                Group = ScanGroup.Settings,
                Severity = Severity.Warning,
                Title = "Контроль учётных записей (UAC) отключён",
                Detail = "Нет запроса прав при изменениях системы",
                Explain = "UAC спрашивает разрешение, когда программа хочет изменить систему — это мешает вирусам " +
                          "действовать тихо. Сейчас он отключён. Рекомендуем включить.",
            });
        }

        if (!settings.AutomaticUpdatesEnabled)
        {
            findings.Add(new Finding
            {
                Id = "settings-updates-off",
                Group = ScanGroup.Settings,
                Severity = Severity.Warning,
                Title = "Автообновления Windows отключены",
                Detail = "Система не получает исправления безопасности",
                Explain = "Обновления Windows закрывают дыры в безопасности. Без них компьютер уязвимее. " +
                          "Включим автоматическое обновление.",
            });
        }

        if (settings.RemoteDesktopEnabled)
        {
            findings.Add(new Finding
            {
                Id = "settings-rdp-on",
                Group = ScanGroup.Settings,
                Severity = Severity.Info,
                Title = "Удалённый рабочий стол включён",
                Detail = "К компьютеру можно подключаться по сети",
                Explain = "Удалённый рабочий стол позволяет управлять компьютером по сети. Если ты им не " +
                          "пользуешься — лучше выключить, чтобы снаружи нельзя было подключиться.",
            });
        }

        return new ScanResult { Group = ScanGroup.Settings, Findings = findings };
    }

    /// <summary>
    /// Находка «брандмауэр выключен» с перечислением конкретных профилей сети. Починка включает ровно те
    /// профили, где защита снята (координаты передаются через <see cref="FindingDataKeys"/>).
    /// </summary>
    private static Finding FirewallFinding(IReadOnlyList<string> disabledProfiles)
    {
        var names = disabledProfiles.Select(FirewallProfileName).ToList();
        var where = names.Count > 0 ? string.Join(", ", names) : "во всех сетях";

        var data = new Dictionary<string, string>
        {
            [FindingDataKeys.Kind] = FindingKinds.FirewallEnable,
        };
        if (disabledProfiles.Count > 0)
        {
            data[FindingDataKeys.Profiles] = string.Join(",", disabledProfiles);
        }

        return new Finding
        {
            Id = "settings-firewall-off",
            Group = ScanGroup.Settings,
            Severity = Severity.Danger,
            Title = "Брандмауэр Windows выключен",
            Detail = $"Защита сети отключена: {where}",
            Explain = "Брандмауэр — это «замок на входной двери» компьютера: он не пускает внутрь тех, кто стучится " +
                      $"из сети. Сейчас защита снята — {where}. Это опасно: в общей сети (кафе, отель, общежитие) " +
                      "к компьютеру может подключиться кто угодно. Нажми «Исправить» — включим защиту обратно; " +
                      "интернет и программы от этого не пострадают.",
            Data = data,
        };
    }

    /// <summary>Название профиля брандмауэра простыми словами (человек не знает слов «домен»/«стандартный»).</summary>
    private static string FirewallProfileName(string profileKey) => profileKey switch
    {
        "DomainProfile" => "рабочая сеть",
        "StandardProfile" => "домашняя сеть",
        "PublicProfile" => "общественная сеть (Wi-Fi в кафе, вокзале)",
        _ => profileKey,
    };
}
