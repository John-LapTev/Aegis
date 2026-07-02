using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Что ищем: драйвер устройства или утилиту/ПО для него.</summary>
public enum DeviceLookupKind
{
    Driver,
    Utility,
}

/// <summary>
/// Поиск драйвера/утилиты для устройства в интернете: официальная ссылка на загрузку + приблизительная
/// последняя версия (через веб-поиск). Best-effort: нет сети/результатов → <see cref="DeviceUpdateResult.Empty"/>.
/// </summary>
public interface IDeviceUpdateLookup
{
    Task<DeviceUpdateResult> LookupAsync(
        string deviceName, DeviceLookupKind kind = DeviceLookupKind.Driver, CancellationToken cancellationToken = default);
}
