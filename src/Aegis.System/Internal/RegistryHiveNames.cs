using Microsoft.Win32;

namespace Aegis.System.Internal;

/// <summary>
/// Нормализация имён кустов реестра. Сканеры/находки используют короткие имена (<c>HKLM</c>, <c>HKCU</c>),
/// но <c>reg.exe export</c> требует полные (<c>HKEY_LOCAL_MACHINE</c>). Здесь — единое сопоставление,
/// чтобы бэкап ветки и удаление работали согласованно (ADR 0002).
/// </summary>
internal static class RegistryHiveNames
{
    /// <summary>Полное имя куста для <c>reg.exe</c> (<c>HKLM</c> → <c>HKEY_LOCAL_MACHINE</c>). Полные имена возвращаются как есть.</summary>
    public static string ToFullName(string hive) => hive.Trim().ToUpperInvariant() switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" => "HKEY_LOCAL_MACHINE",
        "HKCU" or "HKEY_CURRENT_USER" => "HKEY_CURRENT_USER",
        "HKCR" or "HKEY_CLASSES_ROOT" => "HKEY_CLASSES_ROOT",
        "HKU" or "HKEY_USERS" => "HKEY_USERS",
        "HKCC" or "HKEY_CURRENT_CONFIG" => "HKEY_CURRENT_CONFIG",
        _ => hive,
    };

    /// <summary>Куст в виде <see cref="RegistryHive"/> для .NET-доступа к реестру.</summary>
    public static RegistryHive ToHive(string hive) => hive.Trim().ToUpperInvariant() switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
        "HKCU" or "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
        "HKCR" or "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
        "HKU" or "HKEY_USERS" => RegistryHive.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
        _ => RegistryHive.CurrentUser,
    };

    /// <summary>Короткое имя куста (<c>HKLM</c>/<c>HKCU</c>…) для компактного хранения в записи бэкапа.</summary>
    public static string ToShortName(RegistryHive hive) => hive switch
    {
        RegistryHive.LocalMachine => "HKLM",
        RegistryHive.CurrentUser => "HKCU",
        RegistryHive.ClassesRoot => "HKCR",
        RegistryHive.Users => "HKU",
        RegistryHive.CurrentConfig => "HKCC",
        _ => "HKCU",
    };
}
