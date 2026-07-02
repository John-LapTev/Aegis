namespace Aegis.Scanners.Probing;

/// <summary>Поиск установленных «лишних» UWP-приложений (встроенный хлам/промо). Только читает.</summary>
public interface IAppxProbe
{
    Task<IReadOnlyList<AppxApp>> FindBloatAsync(CancellationToken cancellationToken = default);
}
