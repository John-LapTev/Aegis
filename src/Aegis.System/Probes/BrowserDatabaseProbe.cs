using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный поиск раздутых баз браузеров: обходит профили из каталога, считает пустое место внутри каждой
/// базы. Базы запущенных браузеров пропускает целиком — их нельзя ни надёжно измерить, ни сжать.
/// Только читает.
/// </summary>
public sealed class BrowserDatabaseProbe : IBrowserDatabaseProbe
{
    public Task<IReadOnlyList<BloatedDatabase>> FindAsync(CancellationToken cancellationToken = default)
    {
        var found = new List<BloatedDatabase>();

        foreach (var browser in BrowserDatabaseCatalog.Browsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SqliteMaintenance.IsAnyProcessRunning(browser.Processes))
            {
                continue; // браузер открыт — сжимать его базы нельзя
            }

            var root = Environment.ExpandEnvironmentVariables(browser.ProfilesRoot);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in EnumerateDatabaseFiles(root, browser.FileNames, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var size = new FileInfo(file).Length;
                if (size < BrowserDatabaseCatalog.MinimumFileSizeBytes)
                {
                    continue;
                }

                var reclaimable = SqliteMaintenance.EstimateReclaimable(file);
                if (reclaimable <= 0)
                {
                    continue;
                }

                found.Add(new BloatedDatabase
                {
                    Path = file,
                    Browser = browser.Name,
                    SizeBytes = size,
                    ReclaimableBytes = reclaimable,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<BloatedDatabase>>(found);
    }

    /// <summary>
    /// Файлы баз внутри профилей. Профили — это подпапки корня (Default, Profile 1, xxxx.default-release),
    /// поэтому ищем и в самом корне, и на один уровень глубже.
    /// </summary>
    private static IEnumerable<string> EnumerateDatabaseFiles(
        string root,
        IReadOnlyList<string> fileNames,
        CancellationToken cancellationToken)
    {
        var directories = new List<string> { root };

        try
        {
            directories.AddRange(Directory.EnumerateDirectories(root));
        }
        catch (Exception)
        {
            // Корень недоступен — вернём то, что есть.
        }

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var name in fileNames)
            {
                string path;
                try
                {
                    path = Path.Combine(directory, name);
                    if (!File.Exists(path))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                yield return path;
            }
        }
    }
}
