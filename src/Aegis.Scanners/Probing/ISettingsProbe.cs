namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик снимка системных настроек. Только ЧИТАЕТ. Windows-реализация — в слое доступа
/// к системе; правила оценки — в <see cref="Settings.SettingsScanner"/>.
/// </summary>
public interface ISettingsProbe
{
    /// <summary>Считать текущие системные настройки.</summary>
    Task<SystemSettingsSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
