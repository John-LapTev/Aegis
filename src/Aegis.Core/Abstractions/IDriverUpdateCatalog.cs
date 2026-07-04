using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Официальный каталог доступных обновлений драйверов Windows (агент Windows Update, WUA) — для сверки версий
/// драйверов ВСЕХ устройств, а не только видеокарты NVIDIA. Возвращает применимые, но ещё не установленные
/// драйверы: их наличие = «доступна более свежая версия». Best-effort: нет сети / не Windows / API недоступен → пусто.
/// </summary>
public interface IDriverUpdateCatalog
{
    Task<IReadOnlyList<DriverUpdateOffer>> GetAvailableAsync(CancellationToken cancellationToken = default);
}
