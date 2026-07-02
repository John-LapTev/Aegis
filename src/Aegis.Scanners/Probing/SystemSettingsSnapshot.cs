namespace Aegis.Scanners.Probing;

/// <summary>Снимок ключевых системных настроек безопасности/обслуживания для группы «Настройки».</summary>
public sealed record SystemSettingsSnapshot
{
    /// <summary>Включён ли брандмауэр Windows.</summary>
    public required bool FirewallEnabled { get; init; }

    /// <summary>Включён ли контроль учётных записей (UAC).</summary>
    public required bool UacEnabled { get; init; }

    /// <summary>Включены ли автоматические обновления Windows.</summary>
    public required bool AutomaticUpdatesEnabled { get; init; }

    /// <summary>Включён ли удалённый рабочий стол (RDP).</summary>
    public required bool RemoteDesktopEnabled { get; init; }
}
