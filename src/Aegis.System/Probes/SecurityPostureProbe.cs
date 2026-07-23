using System.Globalization;
using System.Management;
using Microsoft.Win32;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник «состояния защиты»: шифрование дисков (WMI BitLocker), давность последнего обновления
/// Windows (WMI Win32_QuickFixEngineering), требование пароля при пробуждении и вход по ПИН-коду/лицу
/// (реестр), открытые наружу порты (netstat). Только читает, всё best-effort: чего узнать не вышло —
/// приходит как null, и в интерфейсе честно пишется «не удалось проверить».
/// </summary>
public sealed class SecurityPostureProbe : ISecurityPostureProbe
{
    public async Task<SecurityPosture> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new SecurityPosture();
        }

        return new SecurityPosture
        {
            Volumes = ReadVolumes(),
            DaysSinceLastUpdate = ReadDaysSinceLastUpdate(),
            LockOnResume = ReadLockOnResume(),
            WindowsHelloEnabled = ReadWindowsHello(),
            ListeningPorts = await ReadListeningPortsAsync(cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>Диски и состояние шифрования BitLocker (пространство имён появляется только при поддержке функции).</summary>
    private static IReadOnlyList<EncryptedVolume> ReadVolumes()
    {
        var volumes = new List<EncryptedVolume>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftVolumeEncryption",
                "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume");

            foreach (var item in searcher.Get())
            {
                using var volume = (ManagementObject)item;
                var letter = volume["DriveLetter"]?.ToString();
                if (string.IsNullOrWhiteSpace(letter))
                {
                    continue;
                }

                // ProtectionStatus: 0 — выключено, 1 — включено, 2 — состояние неизвестно.
                var status = ToInt(volume["ProtectionStatus"]);
                volumes.Add(new EncryptedVolume { Mount = letter, Protected = status == 1 });
            }
        }
        catch (Exception)
        {
            // В домашних редакциях Windows BitLocker недоступен — это нормально, просто не показываем плитку.
        }

        return volumes;
    }

    /// <summary>Сколько дней прошло с последнего установленного обновления Windows.</summary>
    private static int? ReadDaysSinceLastUpdate()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT InstalledOn FROM Win32_QuickFixEngineering");
            DateTime? latest = null;

            foreach (var item in searcher.Get())
            {
                using var patch = (ManagementObject)item;
                var raw = patch["InstalledOn"]?.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                // Формат зависит от языка системы, поэтому пробуем и локальный, и инвариантный разбор.
                if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date)
                    || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    if (latest is null || date > latest)
                    {
                        latest = date;
                    }
                }
            }

            if (latest is not DateTime last)
            {
                return null;
            }

            return Math.Max(0, (int)(DateTime.Now.Date - last.Date).TotalDays);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Требуется ли пароль при выходе из заставки или сна.</summary>
    private static bool? ReadLockOnResume()
    {
        try
        {
            // Заставка с паролем.
            var secure = RegistryReader.GetDword(RegistryHive.CurrentUser, @"Control Panel\Desktop", "ScreenSaverIsSecure");
            var active = RegistryReader.GetDword(RegistryHive.CurrentUser, @"Control Panel\Desktop", "ScreenSaveActive");
            if (secure == 1 && active != 0)
            {
                return true;
            }

            // Или требование входа после сна (политика питания).
            var promptOnWake = RegistryReader.GetDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Power\PowerSettings\0e796bdb-100d-47d6-a2d5-f7d2daa51f51", "ACSettingIndex");
            return promptOnWake switch
            {
                1 => true,
                0 => false,
                _ => secure == 1 ? true : (bool?)false,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Настроен ли вход по ПИН-коду, лицу или отпечатку (Windows Hello) для текущего пользователя.</summary>
    private static bool? ReadWindowsHello()
    {
        try
        {
            using var ngc = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\NgcPin\Credentials");
            if (ngc is null)
            {
                return false;
            }

            // global:: — в коде под Aegis.* префикс System.* резолвится в наш Aegis.System (см. правила проекта).
            var currentUserSid = global::System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrEmpty(currentUserSid))
            {
                return ngc.GetSubKeyNames().Length > 0;
            }

            return ngc.GetSubKeyNames().Any(name => string.Equals(name, currentUserSid, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Порты, слушающие входящие подключения. Специально через netstat, а не PowerShell: команда стартует
    /// мгновенно и почти не ест память (приём подсмотрен в Kudu — у них PowerShell отъедал ~80 МБ на опрос).
    /// </summary>
    private static async Task<IReadOnlyList<int>> ReadListeningPortsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await ProcessRunner
                .RunForOutputAsync(ProcessRunner.System("netstat.exe"), "-ano -p tcp", cancellationToken)
                .ConfigureAwait(false);

            return NetstatParser.ParseListeningPorts(output);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static int ToInt(object? value)
    {
        try
        {
            return value is null ? -1 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return -1;
        }
    }
}
