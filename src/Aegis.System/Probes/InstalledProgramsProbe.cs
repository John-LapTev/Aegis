using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник установленных программ: читает ветки «Uninstall» реестра (HKLM 64/32 + HKCU), собирает
/// название/издателя/версию/папку установки/команду удаления/размер и полный путь ключа (для чистки остатков).
/// Отсеивает системные компоненты и обновления Windows (KB…), которые пользователю удалять не нужно. Только читает.
/// </summary>
public sealed class InstalledProgramsProbe : IInstalledProgramsProbe
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public Task<IReadOnlyList<InstalledProgram>> FindAsync(bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var byName = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM|64", byName, includeHidden, cancellationToken);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM|32", byName, includeHidden, cancellationToken);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Default, "HKCU|Default", byName, includeHidden, cancellationToken);

        var result = byName.Values.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return Task.FromResult<IReadOnlyList<InstalledProgram>>(result);
    }

    private static void ReadHive(
        RegistryHive hive, RegistryView view, string scopeTag,
        Dictionary<string, InstalledProgram> into, bool includeHidden, CancellationToken cancellationToken)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(UninstallPath);
            if (uninstall is null)
            {
                return;
            }

            foreach (var subName in uninstall.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var sub = uninstall.OpenSubKey(subName);
                    var program = Parse(sub, scopeTag, subName, includeHidden);
                    if (program is not null && !into.ContainsKey(program.Name))
                    {
                        into[program.Name] = program;
                    }
                }
                catch (Exception)
                {
                    // Отдельная запись недоступна/битая — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Ветка недоступна (не Windows / нет прав) — пропускаем.
        }
    }

    private static InstalledProgram? Parse(RegistryKey? key, string scopeTag, string subName, bool includeHidden)
    {
        if (key is null)
        {
            return null;
        }

        var name = (key.GetValue("DisplayName") as string)?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null; // без названия не показываем (компоненты/патчи)
        }

        // Системные компоненты и обновления Windows — по умолчанию скрываем; по запросу показываем с пометкой.
        var isSystem = IsSystemOrUpdate(key, name);
        if (isSystem && !includeHidden)
        {
            return null;
        }

        var uninstall = (key.GetValue("UninstallString") as string)?.Trim();
        var quiet = (key.GetValue("QuietUninstallString") as string)?.Trim();

        return new InstalledProgram
        {
            Name = name,
            Publisher = (key.GetValue("Publisher") as string)?.Trim(),
            Version = (key.GetValue("DisplayVersion") as string)?.Trim(),
            InstallLocation = NormalizePath((key.GetValue("InstallLocation") as string)?.Trim()),
            UninstallCommand = string.IsNullOrWhiteSpace(uninstall) ? null : uninstall,
            QuietUninstallCommand = string.IsNullOrWhiteSpace(quiet) ? null : quiet,
            EstimatedSizeBytes = ReadEstimatedSize(key),
            InstallDate = ReadInstallDate(key),
            IconPath = (key.GetValue("DisplayIcon") as string)?.Trim() is { Length: > 0 } icon ? icon : null,
            IsSystem = isSystem,
            RegistryKeyPath = $"{scopeTag}|{UninstallPath}\\{subName}",
        };
    }

    /// <summary>
    /// Дата установки из реестра (InstallDate = «YYYYMMDD»); null — если поля нет или формат неожиданный.
    /// Через <c>TryParseExact</c>: кривые даты вроде «20230231» (31 февраля) дают null, а НЕ исключение — иначе кривая
    /// дата в косметическом поле роняла бы всю запись программы, и её нельзя было бы ни увидеть, ни удалить (аудит 2026-07-03).
    /// </summary>
    private static DateOnly? ReadInstallDate(RegistryKey key)
    {
        if (key.GetValue("InstallDate") is not string raw || raw.Length != 8)
        {
            return null;
        }

        return DateOnly.TryParseExact(
                   raw, "yyyyMMdd",
                   global::System.Globalization.CultureInfo.InvariantCulture,
                   global::System.Globalization.DateTimeStyles.None, out var date)
               && date.Year is >= 1990 and <= 2100
            ? date
            : null;
    }

    private static bool IsSystemOrUpdate(RegistryKey key, string name)
    {
        // Компонент системы (SystemComponent=1) — скрываем.
        if (key.GetValue("SystemComponent") is int sc && sc == 1)
        {
            return true;
        }

        // Обновления/патчи Windows: есть родительский ключ, тип «update/hotfix/security update».
        if (!string.IsNullOrEmpty(key.GetValue("ParentKeyName") as string))
        {
            return true;
        }

        var releaseType = (key.GetValue("ReleaseType") as string)?.Trim();
        if (releaseType is "Security Update" or "Update" or "Hotfix" or "ServicePack")
        {
            return true;
        }

        // Обновления вида «Update for … (KB123456)» / «Security Update …».
        if (name.StartsWith("Update for ", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Hotfix ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static long ReadEstimatedSize(RegistryKey key)
    {
        // EstimatedSize — DWORD в КИЛОБАЙТАХ.
        if (key.GetValue("EstimatedSize") is int kb && kb > 0)
        {
            return (long)kb * 1024;
        }

        return 0;
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path.Trim().Trim('"').TrimEnd('\\', '/');
    }
}
