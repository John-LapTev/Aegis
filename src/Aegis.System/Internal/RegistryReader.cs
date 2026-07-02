using Microsoft.Win32;

namespace Aegis.System.Internal;

/// <summary>Безопасное чтение реестра (best-effort): ошибки доступа → null/false.</summary>
internal static class RegistryReader
{
    public static int? GetDword(RegistryHive hive, string subKey, string name)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(name) switch
            {
                int i => i,
                long l => (int)l,
                _ => null,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? GetString(RegistryHive hive, string subKey, string name)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(name)?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool KeyExists(RegistryHive hive, string subKey)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            return key is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool ValueExists(RegistryHive hive, string subKey, string name)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(name) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
