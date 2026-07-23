using Microsoft.Win32;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>Реальный пробник системных настроек безопасности (реестр). Только читает.</summary>
public sealed class SettingsProbe : ISettingsProbe
{
    public Task<SystemSettingsSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        // Профилей брандмауэра ТРИ, и выключить можно любой по отдельности. Раньше читался только частный
        // (StandardProfile) — выключенная защита в публичных сетях (кафе, вокзал), самая опасная, оставалась
        // незамеченной (найдено при разборе Kudu, 2026-07-23).
        var disabledProfiles = FirewallProfiles
            .Where(profile => RegistryReader.GetDword(RegistryHive.LocalMachine,
                FirewallProfileKey(profile.Key), "EnableFirewall") == 0)
            .Select(profile => profile.Key)
            .ToList();

        var uac = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA");

        var noAutoUpdate = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate");

        var denyRdp = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections");

        var snapshot = new SystemSettingsSnapshot
        {
            // Отсутствие значения трактуем как «включено» (поведение Windows по умолчанию).
            FirewallEnabled = disabledProfiles.Count == 0,
            DisabledFirewallProfiles = disabledProfiles,
            UacEnabled = uac != 0,
            AutomaticUpdatesEnabled = noAutoUpdate != 1,
            RemoteDesktopEnabled = denyRdp == 0 && denyRdp is not null,
        };

        return Task.FromResult(snapshot);
    }

    /// <summary>Профили брандмауэра Windows: ключ реестра → название простыми словами.</summary>
    internal static readonly IReadOnlyList<KeyValuePair<string, string>> FirewallProfiles =
    [
        new("DomainProfile", "рабочая сеть"),
        new("StandardProfile", "частная сеть (дом)"),
        new("PublicProfile", "общественная сеть (Wi-Fi в кафе, вокзале)"),
    ];

    /// <summary>Путь в реестре к настройкам профиля брандмауэра.</summary>
    internal static string FirewallProfileKey(string profile) =>
        $@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{profile}";
}
