namespace Aegis.Core.Models;

/// <summary>Итог «грубого» удаления файла/папки: получилось ли и какие мешавшие процессы пришлось завершить.</summary>
public sealed record ForceDeleteResult
{
    /// <summary>Файл/папка перемещены в Корзину.</summary>
    public required bool Success { get; init; }

    /// <summary>Понятное пользователю сообщение — на русском.</summary>
    public required string Message { get; init; }

    /// <summary>Названия процессов, которые пришлось завершить, чтобы освободить файл.</summary>
    public IReadOnlyList<string> KilledProcesses { get; init; } = [];

    public static ForceDeleteResult Failed(string message) => new() { Success = false, Message = message };
}
