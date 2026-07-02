using Microsoft.Win32;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>Реальный пробник системных настроек безопасности (реестр). Только читает.</summary>
public sealed class SettingsProbe : ISettingsProbe
{
    public Task<SystemSettingsSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var firewall = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "EnableFirewall");

        var uac = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA");

        var noAutoUpdate = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate");

        var denyRdp = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections");

        var snapshot = new SystemSettingsSnapshot
        {
            // Отсутствие значения трактуем как «включено» (поведение Windows по умолчанию).
            FirewallEnabled = firewall != 0,
            UacEnabled = uac != 0,
            AutomaticUpdatesEnabled = noAutoUpdate != 1,
            RemoteDesktopEnabled = denyRdp == 0 && denyRdp is not null,
        };

        return Task.FromResult(snapshot);
    }
}
