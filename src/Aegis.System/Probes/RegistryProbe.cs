using Microsoft.Win32;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник реестра: записи об удалении программ, чья папка установки уже не существует
/// (осиротевшие записи). Консервативно — флагует только при заданном и отсутствующем InstallLocation.
/// </summary>
public sealed class RegistryProbe : IRegistryProbe
{
    public Task<IReadOnlyList<RegistryIssue>> FindAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<RegistryIssue>();

        Scan(issues, RegistryHive.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", cancellationToken);
        Scan(issues, RegistryHive.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", cancellationToken);
        Scan(issues, RegistryHive.CurrentUser, "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", cancellationToken);

        return Task.FromResult<IReadOnlyList<RegistryIssue>>(issues);
    }

    private static void Scan(List<RegistryIssue> issues, RegistryHive hive, string hiveName, string subKey, CancellationToken cancellationToken)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            if (key is null)
            {
                return;
            }

            foreach (var name in key.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var entry = key.OpenSubKey(name);
                    var installLocation = NormalizeLocation(entry?.GetValue("InstallLocation")?.ToString());
                    if (installLocation is not null && IsOrphaned(installLocation))
                    {
                        issues.Add(new RegistryIssue
                        {
                            Hive = hiveName,
                            Path = $@"{subKey}\{name}",
                            Kind = RegistryIssueKind.OrphanedUninstallEntry,
                            Reference = installLocation,
                        });
                    }
                }
                catch (Exception)
                {
                    // Подключ недоступен — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Ветка недоступна — пропускаем.
        }
    }

    /// <summary>Убрать обрамляющие кавычки и пробелы у пути из реестра; null — если пусто.</summary>
    private static string? NormalizeLocation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim().Trim('"').Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// Осиротела ли запись (папки программы нет). Не помечаем, если судить НЕЛЬЗЯ: сетевой путь (сервер офлайн)
    /// или съёмный/неготовый диск (вынули флешку) — отсутствие там не значит, что программа удалена.
    /// </summary>
    private static bool IsOrphaned(string location)
    {
        if (location.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false; // UNC/сетевой путь — не судим
        }

        var root = Path.GetPathRoot(location);
        if (!string.IsNullOrEmpty(root))
        {
            try
            {
                var drive = new DriveInfo(root);
                if (!drive.IsReady
                    || drive.DriveType is DriveType.Removable or DriveType.Network or DriveType.NoRootDirectory)
                {
                    return false; // диск не готов/съёмный — не можем уверенно сказать «осиротело»
                }
            }
            catch (Exception)
            {
                return false; // непонятный корень пути — не помечаем
            }
        }

        return !Directory.Exists(location);
    }
}
