using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Settings;

/// <summary>
/// Состояние защиты компьютера (группа <see cref="ScanGroup.Settings"/>): зашифрован ли диск, давно ли
/// приходили обновления, запрашивается ли пароль при пробуждении, настроен ли вход по ПИН-коду, и какие
/// порты открыты наружу. Только показывает — каждая находка объясняет простыми словами, надо ли волноваться.
/// </summary>
public sealed class SecurityPostureScanner : IScanner
{
    /// <summary>После этого срока без обновлений уже стоит забеспокоиться.</summary>
    private const int StaleUpdateDays = 45;

    /// <summary>Порты, открытые «по умолчанию» самой Windows: пугать ими человека незачем.</summary>
    private static readonly IReadOnlySet<int> CommonWindowsPorts = new HashSet<int> { 135, 139, 445, 5040, 7680 };

    private readonly ISecurityPostureProbe _probe;

    public SecurityPostureScanner(ISecurityPostureProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var posture = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        if (UpdateFreshnessFinding(posture.DaysSinceLastUpdate) is { } updates)
        {
            findings.Add(updates);
        }

        if (EncryptionFinding(posture.Volumes) is { } encryption)
        {
            findings.Add(encryption);
        }

        if (LockFinding(posture) is { } screenLock)
        {
            findings.Add(screenLock);
        }

        if (OpenPortsFinding(posture.ListeningPorts) is { } ports)
        {
            findings.Add(ports);
        }

        return new ScanResult { Group = ScanGroup.Settings, Findings = findings };
    }

    /// <summary>Давно ли ставились обновления Windows.</summary>
    private static Finding? UpdateFreshnessFinding(int? daysSinceUpdate)
    {
        if (daysSinceUpdate is not int days || days < StaleUpdateDays)
        {
            return null; // обновлялись недавно (или узнать не удалось) — не тревожим
        }

        var months = days / 30;
        return new Finding
        {
            Id = "posture-updates-stale",
            Group = ScanGroup.Settings,
            Severity = days >= 120 ? Severity.Danger : Severity.Warning,
            Title = "Обновления Windows давно не приходили",
            Detail = $"последнее — {days} дн. назад",
            Explain = $"Последнее обновление системы установлено {days} дней назад" +
                      (months >= 2 ? $" (это около {months} месяцев)" : string.Empty) +
                      ". Обновления закрывают дыры, через которые проникают вирусы, поэтому такой перерыв — плохо. " +
                      "Причины бывают разные: обновления отключили, служба обновления не работает или не хватает " +
                      "места на диске. Загляни в «Параметры → Центр обновления Windows» и нажми «Проверить наличие " +
                      "обновлений». Если там ошибка — проверь в разделе «Настройки», не стоит ли запрет на обновления.",
            Data = new Dictionary<string, string> { [FindingDataKeys.Section] = "Состояние защиты" },
        };
    }

    /// <summary>Зашифрован ли системный диск (BitLocker).</summary>
    private static Finding? EncryptionFinding(IReadOnlyList<EncryptedVolume> volumes)
    {
        if (volumes.Count == 0)
        {
            return null; // BitLocker недоступен (домашняя редакция) — плитку не показываем
        }

        var unprotected = volumes.Where(v => !v.Protected).Select(v => v.Mount).ToList();
        if (unprotected.Count == 0)
        {
            return new Finding
            {
                Id = "posture-encryption",
                Group = ScanGroup.Settings,
                Severity = Severity.Ok,
                Title = "Диск зашифрован",
                Detail = "BitLocker включён",
                Explain = "Данные на диске зашифрованы: если компьютер украдут или потеряется ноутбук, файлы никто не " +
                          "прочитает. Это хорошо, ничего делать не нужно.",
                Data = new Dictionary<string, string> { [FindingDataKeys.Section] = "Состояние защиты" },
            };
        }

        return new Finding
        {
            Id = "posture-encryption",
            Group = ScanGroup.Settings,
            Severity = Severity.Info,
            Title = "Диск не зашифрован",
            Detail = string.Join(", ", unprotected),
            Explain = "Файлы на диске лежат в открытом виде. Пока компьютер у тебя дома — это не проблема. Но если " +
                      "это ноутбук, который ездит с тобой, шифрование (BitLocker) стоит включить: тогда при краже " +
                      "или потере никто не прочитает документы, фотографии и пароли. Включается в «Параметры → " +
                      "Конфиденциальность и защита → Шифрование устройства». Важно: перед включением сохрани ключ " +
                      "восстановления — без него доступ к своим же файлам можно потерять.",
            Data = new Dictionary<string, string> { [FindingDataKeys.Section] = "Состояние защиты" },
        };
    }

    /// <summary>Спрашивают ли пароль при возвращении к компьютеру.</summary>
    private static Finding? LockFinding(SecurityPosture posture)
    {
        if (posture.LockOnResume is not false)
        {
            return null; // пароль спрашивается или узнать не удалось
        }

        var hello = posture.WindowsHelloEnabled == true
            ? " У тебя настроен быстрый вход (ПИН-код или лицо), так что блокировка не будет неудобной."
            : string.Empty;

        return new Finding
        {
            Id = "posture-lock",
            Group = ScanGroup.Settings,
            Severity = Severity.Info,
            Title = "Компьютер не просит пароль после простоя",
            Detail = "вход без подтверждения",
            Explain = "Когда компьютер просыпается, он сразу пускает в систему без пароля. Дома это удобно, но если " +
                      "компьютер бывает в местах, где к нему может подойти посторонний, — лучше включить запрос " +
                      $"пароля: «Параметры → Учётные записи → Варианты входа».{hello}",
            Data = new Dictionary<string, string> { [FindingDataKeys.Section] = "Состояние защиты" },
        };
    }

    /// <summary>Порты, открытые наружу сверх обычных для Windows.</summary>
    private static Finding? OpenPortsFinding(IReadOnlyList<int> listeningPorts)
    {
        var unusual = listeningPorts.Where(port => !CommonWindowsPorts.Contains(port) && port < 49152).ToList();
        if (unusual.Count == 0)
        {
            return null;
        }

        var shown = string.Join(", ", unusual.Take(8));
        var tail = unusual.Count > 8 ? $" и ещё {unusual.Count - 8}" : string.Empty;

        return new Finding
        {
            Id = "posture-open-ports",
            Group = ScanGroup.Settings,
            Severity = Severity.Info,
            Title = "Программы ждут подключений из сети",
            Detail = $"открыто портов: {unusual.Count}",
            Explain = $"Некоторые программы держат «двери» открытыми, чтобы к ним могли подключиться по сети " +
                      $"(номера: {shown}{tail}). Обычно это нормально: так работают игры, торренты, удалённый доступ, " +
                      "программы для локальной сети. Тревожиться стоит, только если ты ничего такого не ставил — тогда " +
                      "загляни во вкладку «Угрозы»: там видно, какая именно программа держит подключение. Брандмауэр " +
                      "при этом всё равно защищает: без разрешения снаружи не подключиться.",
            Data = new Dictionary<string, string> { [FindingDataKeys.Section] = "Состояние защиты" },
        };
    }
}
