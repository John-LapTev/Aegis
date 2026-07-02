namespace Aegis.Scanners.Probing;

/// <summary>Категория мусорных файлов (для группировки находок и понятных подписей).</summary>
public enum JunkCategory
{
    /// <summary>Временные файлы (%TEMP%, Windows\Temp).</summary>
    TempFiles,

    /// <summary>Корзина.</summary>
    RecycleBin,

    /// <summary>Кэш приложений/браузеров.</summary>
    Cache,

    /// <summary>Старые журналы/логи.</summary>
    Logs,

    /// <summary>Кэш обновлений Windows.</summary>
    WindowsUpdateCache,

    /// <summary>Кэш миниатюр.</summary>
    ThumbnailCache,
}
