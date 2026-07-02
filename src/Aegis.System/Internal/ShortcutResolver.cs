using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Aegis.System.Internal;

/// <summary>Разворачивает ярлык (.lnk) в путь целевой программы через WScript.Shell. Только на Windows.</summary>
[SupportedOSPlatform("windows")]
internal static class ShortcutResolver
{
    public static string? ResolveTarget(string shortcutPath)
    {
        object? shell = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null)
            {
                return null;
            }

            shell = Activator.CreateInstance(type);
            if (shell is null)
            {
                return null;
            }

            dynamic wsh = shell;
            dynamic shortcut = wsh.CreateShortcut(shortcutPath);
            string target = shortcut.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception)
        {
            // COM недоступен (не Windows) или ошибка — не разворачиваем.
            return null;
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
