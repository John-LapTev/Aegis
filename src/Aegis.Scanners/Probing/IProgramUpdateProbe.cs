using Aegis.Scanners.Internal;

namespace Aegis.Scanners.Probing;

/// <summary>
/// Проверка обновлений установленных программ через встроенный установщик Windows (winget). Только читает.
/// Реализация Windows-специфична.
/// </summary>
public interface IProgramUpdateProbe
{
    /// <summary>Программы, для которых доступна новая версия. Пусто — обновлять нечего или winget недоступен.</summary>
    Task<IReadOnlyList<AvailableUpgrade>> ReadAvailableAsync(CancellationToken cancellationToken = default);
}
