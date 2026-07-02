using System.Management;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник устройств с ошибками: WMI <c>Win32_PnPEntity.ConfigManagerErrorCode</c>. Код 0 — всё
/// хорошо; 22 — устройство выключено пользователем (не проблема); 45 — сейчас не подключено (съёмное, тоже
/// не проблема). Остальные ненулевые коды означают, что устройство/драйвер работает неправильно. Только читает.
/// </summary>
public sealed class DeviceErrorProbe : IDeviceErrorProbe
{
    public Task<IReadOnlyList<string>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");

            foreach (var item in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var device = (ManagementObject)item;
                if (device["ConfigManagerErrorCode"] is null
                    || !int.TryParse(device["ConfigManagerErrorCode"].ToString(), out var code))
                {
                    continue;
                }

                // 22 — выключено пользователем, 45 — сейчас не подключено (съёмное): это НЕ поломки.
                if (code is 22 or 45)
                {
                    continue;
                }

                var name = device["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name) && !problems.Contains(name))
                {
                    problems.Add(name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Нет WMI / не Windows — считаем, что проблемных устройств не нашли.
        }

        return Task.FromResult<IReadOnlyList<string>>(problems);
    }
}
