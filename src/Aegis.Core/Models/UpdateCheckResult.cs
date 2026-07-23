namespace Aegis.Core.Models;

/// <summary>
/// Итог проверки обновления. Различает «новой версии нет» и «проверить НЕ УДАЛОСЬ» — раньше оба случая
/// выглядели одинаково («обновлений нет»), и человек, у которого проверка молча падала (нет сети, лимит
/// запросов к GitHub), был уверен, что у него свежая версия (жалоба Ивана 1361).
/// </summary>
public sealed record UpdateCheckResult
{
    /// <summary>Сведения о новой версии; null — новой версии нет либо проверка не удалась.</summary>
    public UpdateInfo? Update { get; init; }

    /// <summary>Проверку выполнить не удалось (сеть, лимит запросов, ошибка сервиса).</summary>
    public bool Failed { get; init; }

    /// <summary>Причина неудачи простыми словами (только при <see cref="Failed"/>).</summary>
    public string? Message { get; init; }

    /// <summary>Есть новая версия.</summary>
    public static UpdateCheckResult Available(UpdateInfo info) => new() { Update = info };

    /// <summary>Проверка прошла, новой версии нет.</summary>
    public static readonly UpdateCheckResult UpToDate = new();

    /// <summary>Проверка не удалась — с понятной причиной.</summary>
    public static UpdateCheckResult Error(string message) => new() { Failed = true, Message = message };
}
