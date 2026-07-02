namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик папок-кандидатов на «остатки» удалённых программ (читает профиль + список установленных
/// программ). Только ЧИТАЕТ. Классификация и тексты — в <see cref="Programs.ProgramLeftoverScanner"/>.
/// </summary>
public interface ILeftoverProbe
{
    Task<LeftoverSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
