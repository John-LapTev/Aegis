using Microsoft.Win32;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник ограничений Windows: читает значения из каталога <see cref="PolicyCatalog"/> и сообщает
/// те, что реально активны. Только читает.
/// </summary>
public sealed class PolicyProbe : IPolicyProbe
{
    public Task<IReadOnlyList<PolicyRestriction>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var found = new List<PolicyRestriction>();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<PolicyRestriction>>(found);
        }

        foreach (var rule in PolicyCatalog.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hive = string.Equals(rule.Hive, "HKCU", StringComparison.OrdinalIgnoreCase)
                ? RegistryHive.CurrentUser
                : RegistryHive.LocalMachine;

            var value = RegistryReader.GetDword(hive, rule.SubKey, rule.ValueName);
            if (PolicyCatalog.IsBad(rule, value) && value is int current)
            {
                found.Add(new PolicyRestriction
                {
                    Hive = rule.Hive,
                    SubKey = rule.SubKey,
                    ValueName = rule.ValueName,
                    Value = current,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<PolicyRestriction>>(found);
    }
}
