namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик элементов автозапуска (реестр Run, папка автозагрузки, планировщик, службы).
/// Только ЧИТАЕТ. Windows-реализация — в слое доступа к системе; логика
/// <see cref="Autostart.AutostartScanner"/> работает поверх этой абстракции.
/// </summary>
public interface IAutostartProbe
{
    /// <summary>Перечислить элементы автозапуска.</summary>
    Task<IReadOnlyList<AutostartEntry>> FindAsync(CancellationToken cancellationToken = default);
}
