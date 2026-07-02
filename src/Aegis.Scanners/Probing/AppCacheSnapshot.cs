namespace Aegis.Scanners.Probing;

/// <summary>Снимок кэшей установленных приложений (что можно безопасно очистить).</summary>
public sealed record AppCacheSnapshot
{
    public required IReadOnlyList<AppCacheItem> Apps { get; init; }
}

/// <summary>Одна очищаемая категория приложения (кэш / cookie / история).</summary>
public sealed record AppCacheItem
{
    /// <summary>Понятное имя приложения.</summary>
    public required string Name { get; init; }

    /// <summary>Категория — определяет текст, важность и безопасность очистки.</summary>
    public required AppCacheCategory Category { get; init; }

    /// <summary>Цели очистки: для кэша — папки (чистятся файлы внутри), для cookie/истории — файлы.</summary>
    public required IReadOnlyList<string> Targets { get; init; }

    /// <summary>Суммарный размер, байт.</summary>
    public required long Bytes { get; init; }

    /// <summary>Сколько файлов.</summary>
    public required int FileCount { get; init; }
}

/// <summary>Категория очистки приложения.</summary>
public enum AppCacheCategory
{
    /// <summary>Кэш — безопасно, пересоздаётся.</summary>
    Cache,

    /// <summary>Cookie — выход из аккаунтов; чистить по желанию, с предупреждением.</summary>
    Cookies,

    /// <summary>История браузера.</summary>
    History,
}
