using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Итог онлайн/локальной проверки файла: вердикт + понятное пояснение простыми словами.</summary>
public sealed record OnlineCheckResult
{
    public required ReputationVerdict Verdict { get; init; }

    /// <summary>Короткий вывод для человека (что показала проверка).</summary>
    public required string Summary { get; init; }
}

/// <summary>
/// Проверка файла «воронкой»: локальный Защитник Windows (без лимитов, офлайн) + VirusTotal по хэшу
/// (если доступен ключ). Запускается по требованию для подозрительных файлов (процессы/автозапуск).
/// </summary>
public interface IFileReputationCheck
{
    Task<OnlineCheckResult> CheckAsync(string filePath, CancellationToken cancellationToken = default);
}
