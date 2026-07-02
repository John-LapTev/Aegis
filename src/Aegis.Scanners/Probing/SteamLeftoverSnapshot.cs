namespace Aegis.Scanners.Probing;

/// <summary>Снимок остатков игр (Steam): кэши удалённых игр и следы пираток.</summary>
public sealed record SteamLeftoverSnapshot
{
    public required IReadOnlyList<SteamLeftover> Items { get; init; }
}

/// <summary>Остаток игры (папка). Тип определяет, насколько безопасно чистить.</summary>
public sealed record SteamLeftover
{
    /// <summary>Понятное название (например, «Кэш удалённой игры (AppID 730)»).</summary>
    public required string Title { get; init; }

    /// <summary>Полный путь к папке-остатку.</summary>
    public required string Path { get; init; }

    /// <summary>Тип остатка — влияет на безопасность очистки.</summary>
    public required SteamLeftoverKind Kind { get; init; }
}

/// <summary>Тип остатка игры.</summary>
public enum SteamLeftoverKind
{
    /// <summary>Кэш удалённой Steam-игры (шейдеры/совместимость) — безопасно чистить, в т.ч. массово.</summary>
    OrphanCache,

    /// <summary>Следы пиратских копий (CODEX/RUNE/эмуляторы) — осторожно: могут быть сейвы, чистить по одному.</summary>
    CrackResidue,
}
