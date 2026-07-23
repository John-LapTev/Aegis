namespace Aegis.Scanners.Probing;

/// <summary>
/// Чтение «состояния защиты» компьютера: шифрование диска, свежесть обновлений, блокировка экрана,
/// вход по ПИН-коду/лицу, порты, открытые наружу. Только читает. Реализация Windows-специфична.
/// </summary>
public interface ISecurityPostureProbe
{
    Task<SecurityPosture> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Снимок состояния защиты компьютера.</summary>
public sealed record SecurityPosture
{
    /// <summary>Диски с шифрованием BitLocker и их состояние. Пусто — узнать не удалось.</summary>
    public IReadOnlyList<EncryptedVolume> Volumes { get; init; } = [];

    /// <summary>Сколько дней прошло с последнего обновления Windows (null — узнать не удалось).</summary>
    public int? DaysSinceLastUpdate { get; init; }

    /// <summary>Требуется ли пароль при выходе из заставки/сна.</summary>
    public bool? LockOnResume { get; init; }

    /// <summary>Настроен ли вход по ПИН-коду, лицу или отпечатку (Windows Hello).</summary>
    public bool? WindowsHelloEnabled { get; init; }

    /// <summary>Порты, открытые для входящих подключений (номера).</summary>
    public IReadOnlyList<int> ListeningPorts { get; init; } = [];
}

/// <summary>Диск и состояние его шифрования.</summary>
public sealed record EncryptedVolume
{
    /// <summary>Буква диска (например, «C:»).</summary>
    public required string Mount { get; init; }

    /// <summary>Включена ли защита (диск зашифрован и ключи под защитой).</summary>
    public required bool Protected { get; init; }
}
