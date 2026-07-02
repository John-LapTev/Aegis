namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик настроек приватности и каталога «лишнего» Windows. Только ЧИТАЕТ. Windows-реализация
/// (реестр телеметрии/рекламы, состояние служб и задач) — в слое доступа к системе; правила и тексты —
/// в <see cref="Privacy.PrivacyDebloatScanner"/>.
/// </summary>
public interface IPrivacyProbe
{
    /// <summary>Считать снимок приватности/деблоата.</summary>
    Task<PrivacySnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
