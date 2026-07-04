using System.Globalization;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Maintenance;

/// <summary>
/// Инструменты обслуживания (группа <see cref="ScanGroup.System"/>): «Починить системные файлы» (SFC/DISM)
/// и «Сбросить сеть». Это НЕ найденные проблемы, а всегда доступные действия-кнопки (severity Info) —
/// выносим в отдельную подсекцию и подписываем «запускали недавно (дата)», чтобы не путали с реальными находками.
/// </summary>
public sealed class SystemMaintenanceScanner : IScanner
{
    private const string ToolsSection = "Инструменты обслуживания — запускать только при проблемах";
    private readonly IMaintenanceHistoryProbe _history;

    public SystemMaintenanceScanner(IMaintenanceHistoryProbe history)
    {
        ArgumentNullException.ThrowIfNull(history);
        _history = history;
    }

    public ScanGroup Group => ScanGroup.System;

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>
        {
            new()
            {
                Id = "maintenance-sfc-dism",
                Group = ScanGroup.System,
                Severity = Severity.Info,
                Title = "Проверить и починить системные файлы Windows",
                Detail = WithLastRun("Восстановление повреждённых системных файлов Windows", "sfc-dism"),
                Explain = "Это не найденная проблема, а доступный инструмент — он есть всегда. Запускать не обязательно: " +
                          "пригодится, если Windows глючит, что-то не открывается или ведёт себя странно. Тогда нажми «Починить» " +
                          "— Windows сама проверит и заменит повреждённые файлы. Это безопасно, занимает несколько минут. " +
                          "После запуска кнопка остаётся в списке — это нормально, она доступна всегда.",
                Data = new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.SfcDismRepair, [FindingDataKeys.Section] = ToolsSection },
            },
            new()
            {
                Id = "maintenance-network-reset",
                Group = ScanGroup.System,
                Severity = Severity.Info,
                Title = "Сбросить сетевые настройки (если интернет барахлит)",
                Detail = WithLastRun("Возврат сетевых настроек Windows к стандартным", "network-reset"),
                Explain = "Это не проблема, а доступный инструмент. Пригодится, если интернет работает с ошибками, сайты " +
                          "не открываются или подключение «зависает». Нажми «Сбросить» — после этого нужна перезагрузка. " +
                          "Логины и пароли Wi-Fi не теряются. Кнопка остаётся в списке всегда — это нормально.",
                Data = new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.NetworkReset, [FindingDataKeys.Section] = ToolsSection },
            },
        };

        return Task.FromResult(new ScanResult { Group = ScanGroup.System, Findings = findings });
    }

    /// <summary>Добавить к описанию маленькую пометку «✓ запускали DD.MM.YYYY», если инструмент уже запускали.</summary>
    private string WithLastRun(string baseDetail, string toolKey)
    {
        var lastRun = _history.GetLastRun(toolKey);
        return lastRun is { } when_
            ? $"{baseDetail}  ·  ✓ запускали {when_.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}"
            : baseDetail;
    }
}
