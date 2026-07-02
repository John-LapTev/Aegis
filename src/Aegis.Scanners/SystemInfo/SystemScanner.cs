using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Сканер общего здоровья системы (группа <see cref="ScanGroup.System"/>): выключенная «Защита
/// системы» (без неё нет отката — это важно!), нехватка места на дисках, ожидание перезагрузки.
/// </summary>
public sealed class SystemScanner : IScanner
{
    /// <summary>Ниже этой доли свободного места считаем диск переполненным.</summary>
    private const double LowDiskRatio = 0.10;

    private readonly ISystemHealthProbe _probe;

    public SystemScanner(ISystemHealthProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.System;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        if (!snapshot.RestoreProtectionEnabled)
        {
            findings.Add(new Finding
            {
                Id = "system-restore-disabled",
                Group = ScanGroup.System,
                Severity = Severity.Danger,
                Title = "Защита системы выключена — откат недоступен",
                Detail = "Точки восстановления Windows отключены",
                Explain = "Aegis перед каждой починкой делает точку восстановления, чтобы можно было всё вернуть. " +
                          "Сейчас она выключена — значит откат невозможен. Включим защиту системы, чтобы работать безопасно.",
            });
        }

        foreach (var drive in snapshot.Drives)
        {
            if (drive.TotalBytes > 0 && drive.FreeRatio < LowDiskRatio)
            {
                findings.Add(new Finding
                {
                    Id = $"system-low-disk-{drive.Name}",
                    Group = ScanGroup.System,
                    Severity = Severity.Warning,
                    Title = $"Мало места на диске {drive.Name}",
                    Detail = $"{HumanSize.Format(drive.FreeBytes)} свободно из {HumanSize.Format(drive.TotalBytes)}",
                    Explain = "На диске почти не осталось свободного места — из-за этого Windows и программы могут " +
                              "тормозить. Поможет очистка мусора на вкладке «Мусор».",
                });
            }
        }

        // Перезагрузка: показываем явный вердикт в обе стороны (нужна / не нужна), чтобы было сразу понятно.
        if (snapshot.PendingReboot)
        {
            var reason = snapshot.PendingRebootReason ?? "недавние изменения";
            findings.Add(new Finding
            {
                Id = "system-pending-reboot",
                Group = ScanGroup.System,
                Severity = Severity.Warning,
                Title = "Нужна перезагрузка",
                Detail = $"Чтобы завершить: {reason}",
                Explain = $"Компьютеру нужна перезагрузка, чтобы завершить {reason}. Это не срочно и полностью безопасно — " +
                          "просто перезагрузись, когда будет удобно. Кнопка «Перезагрузить» перезагрузит через минуту " +
                          "(успеешь сохранить файлы). Если перед этим делал правки реестра/системы — после перезапуска " +
                          "появится окно «всё работает?»: не подтвердишь — изменения откатятся сами.",
                Data = new Dictionary<string, string> { ["kind"] = FindingKinds.Reboot },
            });
        }
        else
        {
            findings.Add(new Finding
            {
                Id = "system-pending-reboot",
                Group = ScanGroup.System,
                Severity = Severity.Ok,
                Title = "Перезагрузка не нужна",
                Detail = "Все изменения уже применены",
                Explain = "Сейчас перезагружать компьютер не обязательно — нет изменений, ожидающих перезапуска. " +
                          "Если потом что-то поправишь и понадобится перезагрузка, программа об этом напишет здесь.",
            });
        }

        return new ScanResult { Group = ScanGroup.System, Findings = findings };
    }
}
