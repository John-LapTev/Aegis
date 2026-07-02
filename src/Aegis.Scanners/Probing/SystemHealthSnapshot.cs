namespace Aegis.Scanners.Probing;

/// <summary>Снимок здоровья системы для группы «Система».</summary>
public sealed record SystemHealthSnapshot
{
    /// <summary>Диски и их заполненность.</summary>
    public required IReadOnlyList<DriveSpace> Drives { get; init; }

    /// <summary>Включена ли «Защита системы» (точки восстановления). Без неё обратимость невозможна.</summary>
    public required bool RestoreProtectionEnabled { get; init; }

    /// <summary>Ожидается ли перезагрузка для завершения ранее внесённых изменений.</summary>
    public required bool PendingReboot { get; init; }

    /// <summary>Что требует перезагрузки простыми словами (обновления Windows / системные файлы…); null — не требуется.</summary>
    public string? PendingRebootReason { get; init; }
}
