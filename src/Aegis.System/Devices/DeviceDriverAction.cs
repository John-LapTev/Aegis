using System.Diagnostics;
using Aegis.Core.Abstractions;

namespace Aegis.System.Devices;

/// <summary>
/// Перезагрузка/переустановка драйвера устройства через системный <c>pnputil</c> (только Windows, требует прав
/// администратора). «Перезагрузить» (/restart-device) — безопасно, драйвер остаётся. «Переустановить»
/// (/remove-device + /scan-devices) — рискованнее: устройство кратко пропадает, пока Windows ставит драйвер заново.
/// </summary>
public sealed class DeviceDriverAction : IDeviceDriverAction
{
    private static string PnpUtil => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe");

    public async Task<DeviceActionResult> RestartAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return new DeviceActionResult(false, "Не удалось определить устройство.");
        }

        var ok = await RunPnpUtilAsync(cancellationToken, "/restart-device", instanceId).ConfigureAwait(false);
        return ok
            ? new DeviceActionResult(true, "Драйвер перезагружен.")
            : new DeviceActionResult(false, "Не удалось перезагрузить драйвер этого устройства.");
    }

    public async Task<DeviceActionResult> ReinstallAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return new DeviceActionResult(false, "Не удалось определить устройство.");
        }

        // Удаляем устройство…
        var removed = await RunPnpUtilAsync(cancellationToken, "/remove-device", instanceId).ConfigureAwait(false);
        if (!removed)
        {
            return new DeviceActionResult(false, "Не удалось удалить устройство для переустановки. Драйвер не тронут.");
        }

        // …и заставляем Windows заново определить оборудование и поставить драйвер.
        var rescanned = await RunPnpUtilAsync(cancellationToken, "/scan-devices").ConfigureAwait(false);
        return rescanned
            ? new DeviceActionResult(true, "Драйвер переустановлен — Windows определил устройство заново.")
            : new DeviceActionResult(false, "Устройство удалено, но авто-переустановка не сработала. " +
                                            "Перезагрузи компьютер — Windows поставит драйвер сам.");
    }

    /// <summary>Запускает pnputil с аргументами (через ArgumentList — безопасно для ID с «&amp;» и пробелами).
    /// Код 0 — успех, 3010 — успех + нужна перезагрузка.</summary>
    private static async Task<bool> RunPnpUtilAsync(CancellationToken cancellationToken, params string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = PnpUtil,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            // Сливаем вывод pnputil, чтобы не подвиснуть на переполнении буфера (как DISM/SFC).
            var drainOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var drainErr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(drainOut, drainErr).ConfigureAwait(false);
            return process.ExitCode is 0 or 3010;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
