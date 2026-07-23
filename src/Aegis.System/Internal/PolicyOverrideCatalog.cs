using Microsoft.Win32;
using Aegis.System.Backup;

namespace Aegis.System.Internal;

/// <summary>
/// Карта «настройка → политика, которая её перебивает». В Windows значения из веток <c>Policies</c> (групповые
/// политики) имеют приоритет над обычными настройками: пока политика лежит в реестре, обычное значение просто
/// игнорируется системой. Практический смысл для нас — честность: без снятия политики починка запишет значение,
/// отрапортует «Исправлено», а Windows продолжит работать по старому.
///
/// Такие политики чаще всего оставляют чужие «оптимизаторы» и активаторы. Поэтому починка снимает мешающую
/// политику ВМЕСТЕ с правкой — обратимо, прежнее состояние политики попадает в тот же бэкап (приём подсмотрен
/// в Sophia Script: там перед каждой правкой чистится соответствующая ветка Policies).
/// </summary>
internal static class PolicyOverrideCatalog
{
    private const string FirewallPolicyRoot = @"SOFTWARE\Policies\Microsoft\WindowsFirewall";

    /// <summary>Ключ карты: куст + путь + имя значения, приведённые к нижнему регистру.</summary>
    private static readonly Dictionary<string, RegistryValueRef[]> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Брандмауэр: настройка живёт в SharedAccess, но политика WindowsFirewall\<Профиль>\EnableFirewall
        // перебивает её полностью — именно так «оптимизаторы» насовсем выключают защиту.
        [Key(RegistryHive.LocalMachine, FirewallSetting("DomainProfile"), "EnableFirewall")] =
            [new(RegistryHive.LocalMachine, $@"{FirewallPolicyRoot}\DomainProfile", "EnableFirewall")],
        [Key(RegistryHive.LocalMachine, FirewallSetting("StandardProfile"), "EnableFirewall")] =
            [new(RegistryHive.LocalMachine, $@"{FirewallPolicyRoot}\StandardProfile", "EnableFirewall")],
        [Key(RegistryHive.LocalMachine, FirewallSetting("PublicProfile"), "EnableFirewall")] =
            [new(RegistryHive.LocalMachine, $@"{FirewallPolicyRoot}\PublicProfile", "EnableFirewall")],

        // Удалённый рабочий стол: политика fDenyTSConnections в Terminal Services перебивает настройку системы.
        [Key(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections")] =
            [new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services", "fDenyTSConnections")],

        // Восстановление системы: политика DisableSR запрещает точки восстановления, даже если защита включена.
        [Key(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", "RPSessionInterval")] =
            [new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore", "DisableSR")],

        // Схема электропитания: политика ActivePowerScheme прибивает схему намертво — powercfg не поможет
        // (тонкость из Sophia: без снятия политики смена схемы не видна даже в настройках Windows).
        [Key(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme")] =
            [new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Power\PowerSettings", "ActivePowerScheme")],
    };

    /// <summary>
    /// Политики, которые перебивают указанную настройку. Пусто — настройка применится сама по себе.
    /// </summary>
    public static IReadOnlyList<RegistryValueRef> OverridesFor(RegistryHive hive, string subKey, string valueName) =>
        Overrides.TryGetValue(Key(hive, subKey, valueName), out var refs) ? refs : [];

    private static string FirewallSetting(string profile) =>
        $@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{profile}";

    private static string Key(RegistryHive hive, string subKey, string valueName) =>
        $"{hive}|{subKey.Trim('\\')}|{valueName}";
}
