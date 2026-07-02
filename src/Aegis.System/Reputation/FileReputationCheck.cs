using System.Security.Cryptography;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Reputation;

/// <summary>
/// Проверка файла «воронкой» (схема threat-verification): локальный Защитник Windows + VirusTotal по хэшу
/// (если задан ключ). Возвращает понятный простыми словами вердикт. VirusTotal — необязателен.
/// </summary>
public sealed class FileReputationCheck : IFileReputationCheck
{
    private readonly IThreatReputationService? _virusTotal;

    public FileReputationCheck(IThreatReputationService? virusTotal = null)
    {
        _virusTotal = virusTotal;
    }

    public async Task<OnlineCheckResult> CheckAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new OnlineCheckResult { Verdict = ReputationVerdict.Unknown, Summary = "Файл не найден — проверить не удалось." };
        }

        var defender = await DefenderScanner.ScanFileAsync(filePath, cancellationToken).ConfigureAwait(false);

        FileReputation? virusTotal = null;
        if (_virusTotal is not null)
        {
            try
            {
                var hash = await ComputeHashAsync(filePath, cancellationToken).ConfigureAwait(false);
                virusTotal = await _virusTotal.CheckHashAsync(hash, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                virusTotal = null; // нет интернета/ключа/лимит — продолжаем с результатом Защитника.
            }
        }

        return Combine(defender, virusTotal);
    }

    private static OnlineCheckResult Combine(DefenderResult defender, FileReputation? virusTotal)
    {
        var maliciousVt = virusTotal is { Verdict: ReputationVerdict.Malicious };
        var suspiciousVt = virusTotal is { Verdict: ReputationVerdict.Suspicious };
        var vtRateLimited = virusTotal is { Verdict: ReputationVerdict.RateLimited };

        if (defender == DefenderResult.ThreatFound || maliciousVt)
        {
            var who = defender == DefenderResult.ThreatFound ? "Защитник Windows" : "антивирусы VirusTotal";
            return new OnlineCheckResult
            {
                Verdict = ReputationVerdict.Malicious,
                Summary = $"Опасно: {who} считает файл угрозой. Рекомендуем остановить процесс и удалить файл.",
            };
        }

        if (suspiciousVt)
        {
            // Счётчик показываем ТОЛЬКО когда он ненулевой: у «подозрительных» malicious часто = 0 (насторожил
            // отдельный признак, а не движки), и «0 из N» читалось бы как «никто не пометил» — вводит в заблуждение.
            var count = virusTotal!.MaliciousCount > 0
                ? $" ({virusTotal.MaliciousCount} из {virusTotal.TotalEngines})"
                : string.Empty;
            return new OnlineCheckResult
            {
                Verdict = ReputationVerdict.Suspicious,
                Summary = $"Подозрительно: часть антивирусов насторожилась{count}. Лучше проверить вручную.",
            };
        }

        var defenderClean = defender == DefenderResult.Clean;
        var vtClean = virusTotal is { Verdict: ReputationVerdict.Clean };

        if (defenderClean || vtClean)
        {
            // Коротко (правка 765): экономим место — длинное перечисление не нужно.
            return new OnlineCheckResult
            {
                Verdict = ReputationVerdict.Clean,
                Summary = "Безопасно, антивирус угроз не обнаружил.",
            };
        }

        if (vtRateLimited)
        {
            return new OnlineCheckResult
            {
                Verdict = ReputationVerdict.Unknown,
                Summary = "VirusTotal пропустил файл из-за лимита запросов, а Защитник Windows недоступен. " +
                          "Это не значит, что файл опасен — повтори проверку чуть позже.",
            };
        }

        return new OnlineCheckResult
        {
            Verdict = ReputationVerdict.Unknown,
            Summary = "Не удалось проверить онлайн (нет Защитника или интернета). Это не значит, что файл опасен — данных просто нет.",
        };
    }

    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
