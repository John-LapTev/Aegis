using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Threats;

/// <summary>
/// Подозрительные службы (группа <see cref="ScanGroup.Threats"/>): запускаются из Temp/AppData/папок пользователя.
/// Без подписи — «Опасно» (типичный приём малвари/майнеров), с подписью — «Внимание» (необычно, стоит проверить).
/// Сама служба не трогается автоматически — даём понятные шаги.
/// </summary>
public sealed class SuspiciousServiceScanner : IScanner
{
    private readonly ISuspiciousServiceProbe _probe;

    public SuspiciousServiceScanner(ISuspiciousServiceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Threats;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var services = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = services.Select(CreateFinding).ToList();
        return new ScanResult { Group = ScanGroup.Threats, Findings = findings };
    }

    private static Finding CreateFinding(SuspiciousService service) => new()
    {
        Id = $"suspicious-service-{service.Name}",
        Group = ScanGroup.Threats,
        Severity = service.Signed ? Severity.Warning : Severity.Danger,
        Title = service.Signed
            ? $"Необычная служба: {service.DisplayName}"
            : $"Подозрительная служба: {service.DisplayName}",
        Detail = $"{service.Reason} — {service.BinaryPath}",
        Explain = service.Signed
            ? $"Служба «{service.DisplayName}» {service.Reason}. Обычные программы ставят службы в защищённые " +
              "системные папки. Файл подписан, поэтому скорее всего это какая-то программа — но место необычное. " +
              "Если ты её узнаёшь — всё в порядке; если нет — проверь компьютер антивирусом."
            : $"Служба «{service.DisplayName}» {service.Reason}, и её файл БЕЗ цифровой подписи. Это типичный " +
              "приём, которым прячутся вирусы и майнеры (запуск в фоне при каждом включении). Настоятельно " +
              "проверь компьютер антивирусом (полная проверка Защитником Windows). Если не узнаёшь эту службу — её " +
              "лучше отключить.",
    };
}
