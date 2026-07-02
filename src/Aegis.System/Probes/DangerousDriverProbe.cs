using System.Collections.Concurrent;
using System.Management;
using System.Security.Cryptography;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Ищет ОПАСНЫЕ драйверы среди загруженных: берёт пути из Win32_SystemDriver, считает SHA-256 каждого файла
/// (параллельно) и сверяет с встроенной базой LOLDrivers. Совпадение по хэшу — точное, без ложных тревог.
/// Только читает.
/// </summary>
public sealed class DangerousDriverProbe : IDangerousDriverProbe
{
    public async Task<IReadOnlyList<DangerousDriver>> FindAsync(CancellationToken cancellationToken = default)
    {
        var database = LolDriversDatabase.Instance;
        if (database.Count == 0)
        {
            return [];
        }

        var drivers = new List<(string Name, string Path)>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, PathName FROM Win32_SystemDriver");
            foreach (var item in searcher.Get())
            {
                using var driver = (ManagementObject)item;
                var path = NormalizePath(driver["PathName"]?.ToString());
                if (path is not null)
                {
                    var name = driver["Name"]?.ToString();
                    drivers.Add((string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name, path));
                }
            }
        }
        catch (Exception)
        {
            return []; // WMI недоступен (не Windows) — пусто.
        }

        var found = new ConcurrentBag<DangerousDriver>();
        await Parallel.ForEachAsync(
            drivers,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
            (driver, token) =>
            {
                var sha256 = TryHash(driver.Path);
                if (sha256 is not null && database.Lookup(sha256) is { } entry)
                {
                    found.Add(new DangerousDriver
                    {
                        Name = entry.Name.Length > 0 ? entry.Name : driver.Name,
                        Path = driver.Path,
                        Malicious = entry.Malicious,
                    });
                }

                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        return found.ToList();
    }

    /// <summary>Привести PathName драйвера к обычному пути: убрать «\??\», развернуть относительный (system32\drivers\...).</summary>
    private static string? NormalizePath(string? pathName)
    {
        if (string.IsNullOrWhiteSpace(pathName))
        {
            return null;
        }

        var path = pathName.Trim();
        if (path.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            path = path[4..];
        }

        // Относительный путь (например, «system32\drivers\xyz.sys») — от каталога Windows.
        if (!Path.IsPathRooted(path))
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            path = Path.Combine(windows, path);
        }

        return File.Exists(path) ? path : null;
    }

    private static string? TryHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }
        catch (Exception)
        {
            return null; // занят/нет прав — пропускаем.
        }
    }
}
