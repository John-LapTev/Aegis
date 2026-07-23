namespace Aegis.Core.Models;

/// <summary>
/// Что именно делать при включении игрового режима. Каждый пункт — отдельная галочка в интерфейсе, чтобы
/// человек сам решал, насколько глубоко трогать систему. Все действия ВРЕМЕННЫЕ: при выключении режима
/// система возвращается ровно в прежнее состояние (см. <see cref="GameModeSnapshot"/>).
/// </summary>
public sealed record GameModeOptions
{
    /// <summary>Приостановить тяжёлые фоновые службы (поиск, SysMain, обновления, печать, телеметрия).</summary>
    public bool PauseServices { get; init; } = true;

    /// <summary>Закрыть фоновые программы: браузеры, мессенджеры, обновляторы.</summary>
    public bool CloseBackgroundApps { get; init; } = true;

    /// <summary>Переключить схему электропитания на «Высокую производительность».</summary>
    public bool HighPerformancePower { get; init; } = true;

    /// <summary>Отключить игровую панель и запись видео Xbox Game Bar (съедает кадры).</summary>
    public bool DisableGameBar { get; init; } = true;

    /// <summary>Не давать компьютеру засыпать и гасить экран во время игры.</summary>
    public bool KeepAwake { get; init; } = true;

    /// <summary>Убрать сетевую задержку (алгоритм Нейгла) — для сетевых игр.</summary>
    public bool ReduceNetworkLatency { get; init; } = true;

    /// <summary>Отключить прозрачность окон Windows — мелочь, но снимает часть нагрузки с видеокарты.</summary>
    public bool DisableTransparency { get; init; }

    /// <summary>Включать режим автоматически, когда запускается игра, и выключать при выходе из неё.</summary>
    public bool AutoDetectGames { get; init; }

    /// <summary>Дополнительные имена игровых процессов от пользователя (например, «mygame.exe»).</summary>
    public IReadOnlyList<string> CustomGameProcesses { get; init; } = [];
}
