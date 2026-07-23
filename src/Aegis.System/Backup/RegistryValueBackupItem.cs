namespace Aegis.System.Backup;

/// <summary>
/// Прежнее состояние ОДНОГО значения реестра внутри бэкапа. Нужен для правок, которые меняют несколько
/// значений сразу (например, брандмауэр включается в трёх профилях): откат обязан вернуть их все, поэтому
/// они лежат в одной записи бэкапа под одним идентификатором, а не тремя независимыми.
/// </summary>
public sealed record RegistryValueBackupItem
{
    public required string Hive { get; init; }
    public required string SubKey { get; init; }
    public required string ValueName { get; init; }

    /// <summary>Существовало ли значение до правки (иначе при откате его нужно удалить).</summary>
    public required bool Existed { get; init; }

    /// <summary>Тип значения (имя <see cref="Microsoft.Win32.RegistryValueKind"/>), если было.</summary>
    public string? ValueKind { get; init; }

    /// <summary>Прежнее значение в строковом виде.</summary>
    public string? Value { get; init; }
}
