namespace Aegis.Scanners.Probing;

/// <summary>Подозрительное сетевое подключение процесса (read-only). Причину определяет пробник.</summary>
public sealed record SuspiciousConnection
{
    /// <summary>Имя процесса, установившего соединение.</summary>
    public required string ProcessName { get; init; }

    /// <summary>PID процесса-владельца (0 — не определён) — чтобы можно было остановить.</summary>
    public int ProcessId { get; init; }

    /// <summary>Удалённый адрес.</summary>
    public required string RemoteAddress { get; init; }

    /// <summary>Удалённый порт.</summary>
    public required int RemotePort { get; init; }

    /// <summary>Короткая причина подозрения (например, «известный пул майнинга», «без подписи, исходящее»).</summary>
    public required string Reason { get; init; }
}
