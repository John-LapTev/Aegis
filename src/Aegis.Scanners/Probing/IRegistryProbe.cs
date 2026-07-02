namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик проблем реестра. Только ЧИТАЕТ. Windows-реализация (Microsoft.Win32.Registry) —
/// в слое доступа к системе; логика <see cref="Registry.RegistryScanner"/> работает поверх абстракции.
/// </summary>
public interface IRegistryProbe
{
    /// <summary>Найти проблемные записи реестра.</summary>
    Task<IReadOnlyList<RegistryIssue>> FindAsync(CancellationToken cancellationToken = default);
}
