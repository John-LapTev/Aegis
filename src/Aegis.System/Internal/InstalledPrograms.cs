using Microsoft.Win32;

namespace Aegis.System.Internal;

/// <summary>
/// Список установленных программ из веток «Uninstall» реестра (HKLM 64/32 + HKCU). Общий хелпер для
/// пробников, которым нужно понять, что уже стоит на компьютере (утилиты, остатки удалённых программ).
/// Только читает.
/// </summary>
internal static class InstalledPrograms
{
    /// <summary>Названия установленных программ (DisplayName); при <paramref name="includePublisher"/> — ещё и издатели.</summary>
    public static IReadOnlyList<string> Read(bool includePublisher = false)
    {
        var names = new List<string>();
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, names, includePublisher);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, names, includePublisher);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Default, names, includePublisher);
        return names;
    }

    private static void ReadHive(RegistryHive hive, RegistryView view, List<string> into, bool includePublisher)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null)
            {
                return;
            }

            foreach (var subName in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var sub = uninstall.OpenSubKey(subName);
                    AddValue(sub, "DisplayName", into);
                    if (includePublisher)
                    {
                        AddValue(sub, "Publisher", into);
                    }
                }
                catch (Exception)
                {
                    // Отдельная запись недоступна — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Ветка недоступна (не Windows / нет прав) — пропускаем.
        }
    }

    private static void AddValue(RegistryKey? key, string valueName, List<string> into)
    {
        var text = key?.GetValue(valueName)?.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            into.Add(text);
        }
    }
}
