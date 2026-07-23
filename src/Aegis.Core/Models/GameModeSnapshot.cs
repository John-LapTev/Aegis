namespace Aegis.Core.Models;

/// <summary>
/// Состояние системы ДО включения игрового режима — то, к чему всё вернётся при выключении. Хранится файлом
/// на диске, поэтому режим можно честно выключить даже после перезапуска (или падения) программы: иначе
/// службы остались бы выключенными, а схема питания — переключённой, и человек об этом не узнал бы.
/// </summary>
public sealed record GameModeSnapshot
{
    /// <summary>Когда режим был включён.</summary>
    public required DateTimeOffset ActivatedAt { get; init; }

    /// <summary>Приостановленные службы и их прежнее состояние.</summary>
    public IReadOnlyList<GameModeServiceState> Services { get; init; } = [];

    /// <summary>Изменённые значения реестра (игровая панель, прозрачность, сетевая задержка).</summary>
    public IReadOnlyList<GameModeRegistryState> RegistryValues { get; init; } = [];

    /// <summary>Прежняя схема электропитания (GUID); null — не меняли.</summary>
    public string? PowerSchemeGuid { get; init; }

    /// <summary>Какие программы были закрыты — только чтобы честно показать это человеку (обратно не запускаем).</summary>
    public IReadOnlyList<string> ClosedApps { get; init; } = [];

    /// <summary>Имя процесса игры, из-за которой режим включился автоматически (null — включили вручную).</summary>
    public string? TriggeredByGame { get; init; }
}

/// <summary>Прежнее состояние службы: тип запуска и работала ли она.</summary>
public sealed record GameModeServiceState
{
    public required string Name { get; init; }

    /// <summary>Прежний тип запуска (значение <c>Start</c> в реестре: 2 — авто, 3 — вручную, 4 — отключена).</summary>
    public required int StartType { get; init; }

    /// <summary>Работала ли служба в момент включения режима (иначе запускать её обратно не нужно).</summary>
    public required bool WasRunning { get; init; }
}

/// <summary>Прежнее состояние значения реестра, изменённого игровым режимом.</summary>
public sealed record GameModeRegistryState
{
    public required string Hive { get; init; }
    public required string SubKey { get; init; }
    public required string ValueName { get; init; }

    /// <summary>Прежнее значение; null — значения не было, при откате его нужно удалить.</summary>
    public int? Value { get; init; }
}
