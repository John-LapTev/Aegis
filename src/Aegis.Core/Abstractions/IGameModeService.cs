using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Игровой режим: ВРЕМЕННО освобождает компьютер под игру (приостанавливает фоновые службы и программы,
/// переключает питание на производительность, отключает игровую панель и сетевую задержку) и возвращает
/// всё обратно при выключении. Прежнее состояние пишется на диск ПЕРЕД изменениями, поэтому откат
/// переживает перезапуск программы. Реализация Windows-специфична.
/// </summary>
public interface IGameModeService
{
    /// <summary>Текущее состояние режима (включён/выключен, что применено).</summary>
    Task<GameModeStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Включить режим по выбранным настройкам. Повторный вызов при включённом режиме ничего не меняет.</summary>
    Task<GameModeResult> ActivateAsync(GameModeOptions options, CancellationToken cancellationToken = default);

    /// <summary>Выключить режим и вернуть систему в прежнее состояние.</summary>
    Task<GameModeResult> DeactivateAsync(CancellationToken cancellationToken = default);

    /// <summary>Имя процесса игры, запущенной прямо сейчас (null — игра не найдена). Для авто-режима и подписи.</summary>
    Task<string?> DetectRunningGameAsync(IReadOnlyList<string> customProcesses, CancellationToken cancellationToken = default);
}
