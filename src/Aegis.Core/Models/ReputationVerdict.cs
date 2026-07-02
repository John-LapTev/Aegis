namespace Aegis.Core.Models;

/// <summary>Вердикт репутации файла по онлайн-сверке (VirusTotal).</summary>
public enum ReputationVerdict
{
    /// <summary>Нет данных (файл не найден в базе или сверка недоступна).</summary>
    Unknown,

    /// <summary>Чисто — ни один движок не считает файл вредоносным.</summary>
    Clean,

    /// <summary>Подозрительно — несколько движков насторожились.</summary>
    Suspicious,

    /// <summary>Вредоносно — много движков считают файл угрозой.</summary>
    Malicious,

    /// <summary>Проверка пропущена из-за лимита запросов VirusTotal — данных сейчас нет, можно повторить позже.</summary>
    RateLimited,
}
