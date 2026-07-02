namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик состояния звука (устройства + установленные службы-улучшайзеры). Только ЧИТАЕТ
/// (WMI/реестр). Логика и тексты-рекомендации — в <see cref="Audio.AudioScanner"/>.
/// </summary>
public interface IAudioProbe
{
    Task<AudioSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
