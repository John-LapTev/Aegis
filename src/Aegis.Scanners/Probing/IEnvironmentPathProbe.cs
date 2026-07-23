namespace Aegis.Scanners.Probing;

/// <summary>
/// Чтение переменной среды <c>Path</c> (списка папок для поиска программ) — системной и пользовательской.
/// Только читает. Реализация Windows-специфична.
/// </summary>
public interface IEnvironmentPathProbe
{
    /// <summary>Записи Path, ведущие в несуществующие папки.</summary>
    Task<IReadOnlyList<BrokenPathEntry>> ReadBrokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>Запись переменной Path, ведущая в никуда.</summary>
public sealed record BrokenPathEntry
{
    /// <summary>Папка, которой больше нет.</summary>
    public required string Directory { get; init; }

    /// <summary>Область: <c>HKLM</c> — общая для всего компьютера, <c>HKCU</c> — только для этого пользователя.</summary>
    public required string Hive { get; init; }
}
