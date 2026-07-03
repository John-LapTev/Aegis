namespace Aegis.Core.Models;

/// <summary>
/// Реальные измерения скорости загрузки Windows из журнала «Diagnostics-Performance».
/// В отличие от «влияния» в Диспетчере задач (Низкое/Высокое) — здесь точные секунды.
/// </summary>
public sealed record BootPerformance
{
    /// <summary>Сколько занимает загрузка компьютера целиком (последняя измеренная), если известно.</summary>
    public TimeSpan? BootDuration { get; init; }

    /// <summary>Программы/службы/драйверы, которые заметно тормозят загрузку (от больших к малым).</summary>
    public IReadOnlyList<BootCulprit> Culprits { get; init; } = [];
}

/// <summary>Один «виновник» медленной загрузки — с измеренным добавленным временем.</summary>
public sealed record BootCulprit
{
    /// <summary>Имя программы/службы/драйвера, как записала Windows.</summary>
    public required string Name { get; init; }

    /// <summary>Сколько времени этот элемент добавляет к загрузке.</summary>
    public required TimeSpan Impact { get; init; }

    /// <summary>Что это — программа, служба или драйвер (для понятной подписи).</summary>
    public required BootCulpritKind Kind { get; init; }
}

/// <summary>Тип «виновника» медленной загрузки.</summary>
public enum BootCulpritKind
{
    /// <summary>Программа из автозапуска.</summary>
    Application,

    /// <summary>Служба Windows/стороннего ПО.</summary>
    Service,

    /// <summary>Драйвер устройства.</summary>
    Driver,
}
