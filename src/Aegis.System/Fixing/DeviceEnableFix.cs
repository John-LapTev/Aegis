using System.Diagnostics;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Fixing;

/// <summary>
/// Включает отключённое устройство (например, случайно отключённый микрофон) штатным
/// <c>pnputil /enable-device</c>. Обратимо (устройство можно снова отключить в Windows).
/// </summary>
public sealed class DeviceEnableFix : IFix
{
    private readonly string _deviceId;

    public DeviceEnableFix(string findingId, string deviceId)
    {
        FindingId = findingId;
        _deviceId = deviceId;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Drivers;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("/enable-device");
            startInfo.ArgumentList.Add(_deviceId);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return FixOutcome.Failed("Не удалось включить устройство.");
            }

            // Сливаем вывод pnputil, чтобы не подвиснуть на буфере (как DISM).
            var drainOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var drainErr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(drainOut, drainErr).ConfigureAwait(false);
            // 0 = успех; 3010 = успех, нужна перезагрузка (ERROR_SUCCESS_REBOOT_REQUIRED) — тоже успех.
            return process.ExitCode switch
            {
                0 => FixOutcome.OkWithoutBackup(),
                3010 => FixOutcome.OkWithoutBackup(requiresReboot: true),
                _ => FixOutcome.Failed("Windows не дал включить это устройство. Часто так с виртуальным звуком " +
                                       "(NVIDIA/HDMI) — он работает только при подключённом HDMI-кабеле. Это не страшно, оставь как есть."),
            };
        }
        catch (Exception ex)
        {
            return FixOutcome.Failed("Не удалось включить устройство: " + ex.Message);
        }
    }
}
