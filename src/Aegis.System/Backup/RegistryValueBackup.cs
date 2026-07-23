namespace Aegis.System.Backup;

/// <summary>Сохранённое прежнее состояние значения реестра — для точного отката правки.</summary>
public sealed record RegistryValueBackup
{
    public required string Id { get; init; }
    public required string Hive { get; init; }
    public required string SubKey { get; init; }
    public required string ValueName { get; init; }

    /// <summary>Существовало ли значение до правки (иначе при откате его нужно удалить).</summary>
    public required bool Existed { get; init; }

    /// <summary>Тип значения (имя <see cref="Microsoft.Win32.RegistryValueKind"/>), если было.</summary>
    public string? ValueKind { get; init; }

    /// <summary>Прежнее значение в строковом виде.</summary>
    public string? Value { get; init; }

    /// <summary>
    /// Остальные значения той же правки (когда одна кнопка меняет несколько значений сразу). Пусто у обычных
    /// одиночных бэкапов — так старые файлы бэкапов читаются без изменений.
    /// </summary>
    public IReadOnlyList<RegistryValueBackupItem> Additional { get; init; } = [];

    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
