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

    /// <summary>
    /// Скачать и установить драйвер прямо из программы (по <paramref name="updateId"/> из предложения) — через Windows
    /// Update, без переходов на сайт. Best-effort: вне Windows / не найдено / ошибка WUA → неуспех с сообщением.
    /// </summary>
    Task<DriverInstallResult> InstallAsync(string updateId, CancellationToken cancellationToken = default);
}
