using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Онлайн-проверка репутации файла по хэшу (VirusTotal). Поверх собственной эвристики даёт
/// внешнюю оценку по множеству антивирусных движков (ADR 0003). Ключ API — из окружения/.personal.
/// </summary>
public interface IThreatReputationService
{
    /// <summary>Проверить репутацию файла по его SHA-256.</summary>
    Task<FileReputation> CheckHashAsync(string sha256, CancellationToken cancellationToken = default);
}
