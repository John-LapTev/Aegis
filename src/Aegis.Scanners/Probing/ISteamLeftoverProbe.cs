namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик остатков игр Steam: по базе Steam определяет, какие игры реально установлены, и находит
/// папки удалённых (кэши) + следы пираток. Только ЧИТАЕТ. Классификация/тексты — в
/// <see cref="Programs.SteamLeftoverScanner"/>.
/// </summary>
public interface ISteamLeftoverProbe
{
    Task<SteamLeftoverSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
