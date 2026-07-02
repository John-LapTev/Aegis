namespace Aegis.Core.Models;

/// <summary>Результат онлайн-проверки репутации файла по его хэшу.</summary>
public sealed record FileReputation
{
    /// <summary>SHA-256 файла.</summary>
    public required string Hash { get; init; }

    /// <summary>Итоговый вердикт.</summary>
    public required ReputationVerdict Verdict { get; init; }

    /// <summary>Сколько движков пометили файл как вредоносный.</summary>
    public int MaliciousCount { get; init; }

    /// <summary>Сколько движков всего участвовало в проверке.</summary>
    public int TotalEngines { get; init; }

    /// <summary>Нет данных по этому файлу (не найден в базе).</summary>
    public static FileReputation NotFound(string hash) =>
        new() { Hash = hash, Verdict = ReputationVerdict.Unknown };

    /// <summary>Проверка пропущена из-за лимита запросов VirusTotal (не ходили в сеть/получили 429).</summary>
    public static FileReputation RateLimited(string hash) =>
        new() { Hash = hash, Verdict = ReputationVerdict.RateLimited };
}
