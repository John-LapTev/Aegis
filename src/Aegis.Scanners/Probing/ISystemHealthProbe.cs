namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик данных о здоровье системы (диски, защита восстановления, ожидание ребута).
/// Только ЧИТАЕТ. Windows-реализация — в слое доступа к системе; логика
/// <see cref="SystemInfo.SystemScanner"/> работает поверх этой абстракции.
/// </summary>
public interface ISystemHealthProbe
{
    /// <summary>Считать снимок здоровья системы.</summary>
    Task<SystemHealthSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
