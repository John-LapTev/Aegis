namespace Aegis.Scanners.Probing;

/// <summary>Снимок ключевых системных настроек безопасности/обслуживания для группы «Настройки».</summary>
public sealed record SystemSettingsSnapshot
{
    /// <summary>Включён ли брандмауэр Windows во ВСЕХ профилях сети.</summary>
    public required bool FirewallEnabled { get; init; }

    /// <summary>
    /// Профили брандмауэра, где защита выключена (ключи реестра: <c>DomainProfile</c>, <c>StandardProfile</c>,
    /// <c>PublicProfile</c>). Пусто — включено везде. Нужен, чтобы починка включила именно выключенные профили
    /// и чтобы человеку было видно, в какой сети он не защищён.
    /// </summary>
    public IReadOnlyList<string> DisabledFirewallProfiles { get; init; } = [];

    /// <summary>Включён ли контроль учётных записей (UAC).</summary>
    public required bool UacEnabled { get; init; }

    /// <summary>Включены ли автоматические обновления Windows.</summary>
    public required bool AutomaticUpdatesEnabled { get; init; }

    /// <summary>Включён ли удалённый рабочий стол (RDP).</summary>
    public required bool RemoteDesktopEnabled { get; init; }
}
