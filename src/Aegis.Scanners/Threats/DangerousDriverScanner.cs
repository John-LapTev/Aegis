using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Threats;

/// <summary>
/// Опасные драйверы (группа <see cref="ScanGroup.Threats"/>): сверяет загруженные драйверы по хэшу с базой
/// LOLDrivers. Вредоносные — «Опасно», уязвимые (BYOVD) — «Внимание». Совпадение точное (по SHA-256), поэтому
/// ложных тревог нет. Драйвер работает на уровне ядра, авто-удаление рискованно — поэтому ПРЕДУПРЕЖДАЕМ
/// с понятными шагами, а не удаляем сами.
/// </summary>
public sealed class DangerousDriverScanner : IScanner
{
    private readonly IDangerousDriverProbe _probe;

    public DangerousDriverScanner(IDangerousDriverProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Threats;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var drivers = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = drivers.Select(CreateFinding).ToList();
        return new ScanResult { Group = ScanGroup.Threats, Findings = findings };
    }

    private static Finding CreateFinding(DangerousDriver driver) => new()
    {
        Id = $"dangerous-driver-{driver.Path}",
        Group = ScanGroup.Threats,
        Severity = driver.Malicious ? Severity.Danger : Severity.Warning,
        Title = driver.Malicious
            ? $"Опасный драйвер: {driver.Name}"
            : $"Уязвимый драйвер: {driver.Name}",
        Detail = driver.Path,
        Explain = driver.Malicious
            ? "Этот драйвер есть в базе опасных драйверов (LOLDrivers) — через такие вирусы получают полный контроль " +
              "над системой (уровень ядра) и могут прятаться. НО: некоторые ЛЕГИТИМНЫЕ программы тоже используют такие " +
              "драйверы — VPN, средства обхода блокировок (например «zapret»), программы захвата сетевых пакетов. Если " +
              "ты ставил подобную программу и это её файл (видно по пути) — всё в порядке, нажми «Безопасно». Если нет — " +
              "проверь компьютер антивирусом и удали файл кнопкой ниже."
            : "Этот драйвер известен как УЯЗВИМЫЙ: сам он легитимный (часто даже подписанный), но в нём есть дыра, " +
              "через которую вирусы могут пробраться в ядро Windows (приём «BYOVD»). Если ты не ставил его специально — " +
              "стоит удалить кнопкой ниже или обновить программу, которая его поставила. Если узнаёшь программу — «Безопасно».",
        // Даём реальное действие — удалить файл драйвера (в Корзину). Если файл занят (драйвер загружен) — Windows
        // не даст удалить, пользователю подскажем закрыть программу. Плюс всегда есть «Безопасно» (в исключения).
        Data = new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.FileDelete, ["path"] = driver.Path },
    };
}
