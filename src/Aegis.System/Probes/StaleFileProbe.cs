using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник «залежавшегося»: битые ярлыки (.lnk, цель удалена), пустые (0 байт) файлы в типичных
/// папках и давно не тронутые загрузки. Только читает. Узко по папкам и с лимитами — чтобы быстро и без флуда.
/// </summary>
public sealed class StaleFileProbe : IStaleFileProbe
{
    private const int OldDays = 90;
    private const int PerCategoryLimit = 40;

    public Task<StaleFileSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<StaleFile>();
        AddBrokenShortcuts(items, cancellationToken);
        AddEmptyFiles(items, cancellationToken);
        AddOldDownloads(items, cancellationToken);
        return Task.FromResult(new StaleFileSnapshot { Items = items });
    }

    private static string Downloads =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private static void AddBrokenShortcuts(List<StaleFile> items, CancellationToken cancellationToken)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };

        var found = 0;
        foreach (var root in roots.Where(static r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct())
        {
            try
            {
                foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (found >= PerCategoryLimit)
                    {
                        return;
                    }

                    var target = ShortcutResolver.ResolveTarget(lnk);
                    // Битый только если цель — обычный локальный путь (буква диска) и её НЕТ.
                    // Так не трогаем ярлыки на съёмные/сетевые/UWP-цели (могут быть временно недоступны).
                    if (target is { Length: > 2 } && target[1] == ':'
                        && !File.Exists(target) && !Directory.Exists(target))
                    {
                        items.Add(new StaleFile
                        {
                            Title = Path.GetFileNameWithoutExtension(lnk),
                            Path = lnk,
                            Kind = StaleFileKind.BrokenShortcut,
                        });
                        found++;
                    }
                }
            }
            catch (Exception)
            {
                // Папка недоступна — пропускаем.
            }
        }
    }

    private static void AddEmptyFiles(List<StaleFile> items, CancellationToken cancellationToken)
    {
        var roots = new[]
        {
            Downloads,
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.GetTempPath(),
        };

        var found = 0;
        foreach (var root in roots.Where(static r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct())
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (found >= PerCategoryLimit)
                    {
                        return;
                    }

                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length == 0 && (info.Attributes & FileAttributes.System) == 0)
                        {
                            items.Add(new StaleFile
                            {
                                Title = info.Name,
                                Path = file,
                                Kind = StaleFileKind.EmptyFile,
                            });
                            found++;
                        }
                    }
                    catch (Exception)
                    {
                        // Файл недоступен — пропускаем.
                    }
                }
            }
            catch (Exception)
            {
                // Папка недоступна — пропускаем.
            }
        }
    }

    private static void AddOldDownloads(List<StaleFile> items, CancellationToken cancellationToken)
    {
        var downloads = Downloads;
        if (!Directory.Exists(downloads))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-OldDays);
        var found = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(downloads))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (found >= PerCategoryLimit)
                {
                    return;
                }

                try
                {
                    var info = new FileInfo(file);
                    // Пустые уже учтены отдельно; здесь — непустые и давно не менявшиеся.
                    if (info.Length > 0 && info.LastWriteTimeUtc < cutoff)
                    {
                        var days = (int)(DateTime.UtcNow - info.LastWriteTimeUtc).TotalDays;
                        items.Add(new StaleFile
                        {
                            Title = info.Name,
                            Path = file,
                            Kind = StaleFileKind.OldDownload,
                            Note = $"не менялся {days} дн.",
                        });
                        found++;
                    }
                }
                catch (Exception)
                {
                    // Файл недоступен — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Папка недоступна — пропускаем.
        }
    }
}
