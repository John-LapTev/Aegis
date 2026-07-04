namespace Aegis.Core.Models;

/// <summary>Итог установки драйвера прямо из программы через Windows Update (WUA): успех, нужна ли перезагрузка, сообщение.</summary>
public sealed record DriverInstallResult
{
    public required bool Success { get; init; }

    /// <summary>Требуется перезагрузка, чтобы драйвер применился полностью.</summary>
    public bool RequiresReboot { get; init; }

    /// <summary>Понятное сообщение для пользователя (что произошло / почему не удалось).</summary>
    public string? Message { get; init; }

    public static DriverInstallResult Ok(bool requiresReboot, string? message = null) =>
        new() { Success = true, RequiresReboot = requiresReboot, Message = message };

    public static DriverInstallResult Failed(string message) =>
        new() { Success = false, Message = message };
}
