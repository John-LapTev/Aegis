namespace Aegis.Core.Remediation;

/// <summary>Обстоятельства удаления вредоноса — определяют стратегию (удалить сразу или отложить на ребут).</summary>
public sealed record MinerRemovalContext
{
    /// <summary>Файлы заперты (используются и не удаляются прямо сейчас).</summary>
    public bool FilesLocked { get; init; }

    /// <summary>Вредонос завязан на важный системный процесс — убивать его процесс опасно для системы.</summary>
    public bool TiedToCriticalProcess { get; init; }
}
