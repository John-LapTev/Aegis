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
            findings.Add(new Finding
            {
                Id = "settings-firewall-off",
                Group = ScanGroup.Settings,
                Severity = Severity.Danger,
                Title = "Брандмауэр Windows выключен",
                Detail = "Защита сети отключена",
                Explain = "Брандмауэр защищает компьютер от атак из сети. Сейчас он выключен — это опасно. " +
                          "Включим его обратно.",
            });
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
}
