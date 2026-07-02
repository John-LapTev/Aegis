namespace Aegis.Scanners.Probing;

/// <summary>Активное сетевое подключение процесса (read-only) для эвристик по портам/адресам.</summary>
public sealed record ActiveConnection
{
    /// <summary>Имя процесса.</summary>
    public required string ProcessName { get; init; }

    /// <summary>PID процесса-владельца (0 — не определён) — чтобы можно было остановить.</summary>
    public int ProcessId { get; init; }

    /// <summary>Удалённый адрес.</summary>
    public required string RemoteAddress { get; init; }

    /// <summary>Удалённый порт.</summary>
    public required int RemotePort { get; init; }
}
