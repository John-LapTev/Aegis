namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик кандидатов мусора. Только ЧИТАЕТ файловую систему и измеряет размеры —
/// ничего не удаляет. Windows-реализация (реальные пути %TEMP%, Корзина, кэши) — в слое доступа
/// к системе; логика <see cref="Junk.JunkScanner"/> работает поверх этой абстракции.
/// </summary>
public interface IJunkProbe
{
    /// <summary>Найти мусорные объекты и их размеры.</summary>
    Task<IReadOnlyList<JunkCandidate>> FindAsync(CancellationToken cancellationToken = default);
}
