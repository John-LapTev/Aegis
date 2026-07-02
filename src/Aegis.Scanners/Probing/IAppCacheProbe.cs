namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик кэшей установленных приложений (браузеры, мессенджеры, лаунчеры, шейдеры и т.п.): определяет,
/// что установлено, и сколько занимает кэш. Только читает. Тексты/находки — в <see cref="Programs.AppCacheScanner"/>.
/// </summary>
public interface IAppCacheProbe
{
    Task<AppCacheSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
