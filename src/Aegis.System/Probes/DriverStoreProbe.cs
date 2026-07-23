using System.Management;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник хранилища драйверов: список пакетов через <c>pnputil</c>, список используемых сейчас —
/// через WMI (<c>Win32_PnPSignedDriver</c>). Только читает.
/// </summary>
public sealed class DriverStoreProbe : IDriverStoreProbe
{
    public async Task<IReadOnlyList<DriverPackage>> ReadPackagesAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var pnputil = ProcessRunner.System("pnputil.exe");

        // Современный ключ; на старых сборках Windows 10 работает только «-e».
        var output = await ProcessRunner.RunForOutputAsync(pnputil, "/enum-drivers", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(output))
        {
            output = await ProcessRunner.RunForOutputAsync(pnputil, "-e", cancellationToken).ConfigureAwait(false);
        }

        return DriverPackageParser.Parse(output);
    }

    public Task<IReadOnlySet<string>> ReadActivePackagesAsync(CancellationToken cancellationToken = default)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlySet<string>>(active);
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT InfName FROM Win32_PnPSignedDriver");
            foreach (var item in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var driver = (ManagementObject)item;
                var inf = driver["InfName"]?.ToString();
                if (!string.IsNullOrWhiteSpace(inf))
                {
                    active.Add(inf.Trim().ToLowerInvariant());
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // WMI не ответил: тогда СЧИТАЕМ, что используются все пакеты — лучше ничего не предложить удалить,
            // чем предложить удалить работающий драйвер.
            return Task.FromResult<IReadOnlySet<string>>(AllPackagesGuard);
        }

        return Task.FromResult<IReadOnlySet<string>>(active);
    }

    /// <summary>Особый набор «считать активным всё»: используется, когда список активных получить не удалось.</summary>
    private static readonly IReadOnlySet<string> AllPackagesGuard = new EverythingSet();

    private sealed class EverythingSet : IReadOnlySet<string>
    {
        public int Count => int.MaxValue;

        public bool Contains(string item) => true;

        public bool IsProperSubsetOf(IEnumerable<string> other) => false;

        public bool IsProperSupersetOf(IEnumerable<string> other) => true;

        public bool IsSubsetOf(IEnumerable<string> other) => false;

        public bool IsSupersetOf(IEnumerable<string> other) => true;

        public bool Overlaps(IEnumerable<string> other) => true;

        public bool SetEquals(IEnumerable<string> other) => false;

        public IEnumerator<string> GetEnumerator() => Enumerable.Empty<string>().GetEnumerator();

        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
