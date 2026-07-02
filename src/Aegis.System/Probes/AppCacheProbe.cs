using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник кэшей приложений: по выверенному каталогу <see cref="AppCacheCatalog"/> определяет
/// установленные программы и измеряет их кэш (только чтение). Чистка — обратимая, через обычный мусорный
/// механизм (в Корзину). Узко и с лимитами, чтобы скан не затягивался.
/// </summary>
public sealed class AppCacheProbe : IAppCacheProbe
{
    private const int MaxFilesPerDir = 20000; // ограничение измерения, чтобы не зависнуть на огромном кэше

    public Task<AppCacheSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var apps = new List<AppCacheItem>();

        foreach (var rule in AppCacheCatalog.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Установлено ли — существует хоть одна папка из Detect.
            var installed = rule.Detect.Any(d => AppCachePathExpander.ResolveExistingDirectories(d).Count > 0);
            if (!installed)
            {
                continue;
            }

            foreach (var group in rule.Groups)
            {
                var item = MeasureGroup(rule.Name, group, cancellationToken);
                if (item is not null)
                {
                    apps.Add(item);
                }
            }
        }

        return Task.FromResult(new AppCacheSnapshot { Apps = apps });
    }

    private static AppCacheItem? MeasureGroup(string appName, AppCacheCatalog.CacheGroup group, CancellationToken cancellationToken)
    {
        var targets = new List<string>();
        long bytes = 0;
        var files = 0;

        foreach (var pattern in group.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (group.Kind == AppCacheCatalog.CleanKind.Cache)
            {
                // Кэш — папки: целимся в папку, считаем файлы внутри.
                foreach (var dir in AppCachePathExpander.ResolveExistingDirectories(pattern))
                {
                    var (dirBytes, dirFiles) = Measure(dir, cancellationToken);
                    if (dirFiles > 0)
                    {
                        targets.Add(dir);
                        bytes += dirBytes;
                        files += dirFiles;
                    }
                }
            }
            else
            {
                // Cookie/история — конкретные файлы.
                foreach (var file in AppCachePathExpander.ResolveExistingFiles(pattern))
                {
                    try
                    {
                        bytes += new FileInfo(file).Length;
                        files++;
                        targets.Add(file);
                    }
                    catch (Exception)
                    {
                        // Файл исчез/занят — пропускаем.
                    }
                }
            }
        }

        if (files == 0)
        {
            return null;
        }

        return new AppCacheItem
        {
            Name = appName,
            Category = group.Kind switch
            {
                AppCacheCatalog.CleanKind.Cookies => AppCacheCategory.Cookies,
                AppCacheCatalog.CleanKind.History => AppCacheCategory.History,
                _ => AppCacheCategory.Cache,
            },
            Targets = targets,
            Bytes = bytes,
            FileCount = files,
        };
    }

    private static (long Bytes, int Files) Measure(string dir, CancellationToken cancellationToken)
    {
        long bytes = 0;
        var files = 0;
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            // FileInfo из DirectoryInfo.EnumerateFiles несёт готовый размер — без лишнего syscall на файл.
            foreach (var file in new DirectoryInfo(dir).EnumerateFiles("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (files >= MaxFilesPerDir)
                {
                    break;
                }

                try
                {
                    bytes += file.Length;
                    files++;
                }
                catch (Exception)
                {
                    // Файл исчез/занят — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Папка недоступна — пропускаем.
        }

        return (bytes, files);
    }
}
