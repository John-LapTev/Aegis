namespace Aegis.Scanners.Probing;

/// <summary>Снимок запущенного процесса для анализа (read-only).</summary>
public sealed record ProcessInfo
{
    /// <summary>Идентификатор процесса (PID) — гарантирует уникальность среди одноимённых процессов.</summary>
    public required int ProcessId { get; init; }

    /// <summary>Имя процесса.</summary>
    public required string Name { get; init; }

    /// <summary>Путь к исполняемому файлу (если доступен).</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Статус цифровой подписи.</summary>
    public required SignatureStatus Signature { get; init; }

    /// <summary>Издатель из подписи (если есть).</summary>
    public string? Publisher { get; init; }

    /// <summary>Текущая нагрузка на CPU в процентах (0..100). Для эвристики майнеров.</summary>
    public double CpuPercent { get; init; }
}
