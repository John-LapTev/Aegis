namespace Aegis.Core.Models;

/// <summary>Итог удаления программы: удалилась ли, и что дочищено из остатков (папка/запись реестра).</summary>
public sealed record UninstallResult
{
    /// <summary>Штатный деинсталлятор отработал успешно.</summary>
    public required bool Success { get; init; }

    /// <summary>Понятное пользователю сообщение — на русском.</summary>
    public required string Message { get; init; }

    /// <summary>Пустая папка установки убрана в Корзину (обратимо).</summary>
    public bool RemovedLeftoverFolder { get; init; }

    /// <summary>Осиротевшая запись реестра удалена (с бэкапом ветки перед удалением).</summary>
    public bool RemovedOrphanRegistryKey { get; init; }

    /// <summary>Программа ВСЁ ЕЩЁ числится в системе после деинсталлятора (он вернул «успех», но по факту не удалил —
    /// частый случай с играми/лаунчерами). Значит нужно до-удаление принудительно, а не рапорт «готово».</summary>
    public bool StillRegistered { get; init; }

    public static UninstallResult Failed(string message) => new() { Success = false, Message = message };
}
