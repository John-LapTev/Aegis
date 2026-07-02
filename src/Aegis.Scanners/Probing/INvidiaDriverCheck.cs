namespace Aegis.Scanners.Probing;

/// <summary>
/// Проверка последней версии драйвера видеокарты NVIDIA (через их недокументированный AjaxDriverService).
/// Best-effort: при отсутствии интернета/изменении API возвращает null — тогда остаётся ссылка на офсайт.
/// </summary>
public interface INvidiaDriverCheck
{
    /// <summary>Последняя версия драйвера для видеокарты + признак «новее установленной»; null — узнать не удалось.</summary>
    Task<DriverUpdate?> CheckAsync(string gpuName, string? installedVersion, CancellationToken cancellationToken = default);
}
