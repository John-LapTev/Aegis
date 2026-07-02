namespace Aegis.Core.Abstractions;

/// <summary>
/// Действия над драйвером устройства по его PNPDeviceID: безопасная ПЕРЕЗАГРУЗКА драйвера и полная ПЕРЕУСТАНОВКА.
/// Реализация — через системный pnputil (только Windows, требует прав администратора). За абстракцией ради тестов.
/// </summary>
public interface IDeviceDriverAction
{
    /// <summary>Перезагрузить драйвер устройства (отключить→включить, драйвер остаётся) — безопасно, без потери драйвера.</summary>
    Task<DeviceActionResult> RestartAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>Переустановить драйвер: удалить устройство и заставить Windows определить и поставить заново. Рискованнее —
    /// устройство кратко пропадает. Точку восстановления создаёт вызывающий ПЕРЕД этим.</summary>
    Task<DeviceActionResult> ReinstallAsync(string instanceId, CancellationToken cancellationToken = default);
}

/// <summary>Результат действия над драйвером: успех + понятное сообщение для пользователя (по-русски).</summary>
public sealed record DeviceActionResult(bool Success, string Message);
