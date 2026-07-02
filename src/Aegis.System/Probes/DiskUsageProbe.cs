using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник места на диске: заполненность фиксированных дисков (<see cref="DriveInfo"/>) и
/// крупнейшие папки в профиле пользователя (Загрузки, Документы, AppData, Видео…). Размеры считаем
/// рекурсивно best-effort, недоступные файлы пропускаем. Только читает.
/// </summary>
public sealed class DiskUsageProbe : IDiskUsageProbe
{
    /// <summary>Папки крупнее этого порога попадают в обзор (3 ГБ).</summary>
    private const long LargeFolderThreshold = 3L * 1024 * 1024 * 1024;

    /// <summary>Сколько крупнейших папок показываем максимум.</summary>
    private const int MaxFolders = 12;

    /// <summary>Сколько крупнейших элементов внутри папки показываем в раскрывающемся списке.</summary>
    private const int MaxChildren = 60;

    public Task<DiskUsageSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = new DiskUsageSnapshot
        {
            Drives = ReadDrives(),
            LargeFolders = ReadLargeFolders(cancellationToken),
        };

        return Task.FromResult(snapshot);
    }

    private static IReadOnlyList<DriveSpace> ReadDrives()
    {
        var drives = new List<DriveSpace>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive is { IsReady: true, DriveType: DriveType.Fixed })
                    {
                        drives.Add(new DriveSpace
                        {
                            Name = drive.Name.TrimEnd('\\'),
                            FreeBytes = drive.AvailableFreeSpace,
                            TotalBytes = drive.TotalSize,
                        });
                    }
                }
                catch (Exception)
                {
                    // Диск недоступен — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Не удалось перечислить диски.
        }

        return drives;
    }

    private static IReadOnlyList<FolderUsage> ReadLargeFolders(CancellationToken cancellationToken)
    {
        var folders = new List<FolderUsage>();
        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile) || !Directory.Exists(profile))
            {
                return folders;
            }

            foreach (var dir in Directory.EnumerateDirectories(profile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (size, children) = ReadFolderChildren(dir, cancellationToken);
                if (size >= LargeFolderThreshold)
                {
                    folders.Add(new FolderUsage
                    {
                        Path = dir,
                        SizeBytes = size,
                        Kind = ClassifyFolder(dir),
                        Children = children,
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Профиль недоступен — пусто.
        }

        return folders.OrderByDescending(f => f.SizeBytes).Take(MaxFolders).ToList();
    }

    /// <summary>
    /// Прямые элементы внутри папки (файлы и подпапки) с их размером + суммарный размер всей папки.
    /// Размер подпапки считается рекурсивно. Возвращаем крупнейшие <see cref="MaxChildren"/> для списка,
    /// но суммарный размер — по ВСЕМ элементам. Один проход вместо отдельного подсчёта размера папки.
    /// </summary>
    private static (long TotalBytes, IReadOnlyList<FolderEntry> Children) ReadFolderChildren(
        string folder, CancellationToken cancellationToken)
    {
        var entries = new List<FolderEntry>();
        long total = 0;

        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isDir = Directory.Exists(path);
                long size;
                try
                {
                    size = isDir ? DirectorySize(path, cancellationToken) : new FileInfo(path).Length;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    continue; // элемент исчез/занят — пропускаем
                }

                total += size;
                entries.Add(new FolderEntry
                {
                    Name = Path.GetFileName(path.TrimEnd('\\', '/')),
                    Path = path,
                    SizeBytes = size,
                    IsDirectory = isDir,
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Папка недоступна — вернём то, что успели.
        }

        var children = entries
            .OrderByDescending(static e => e.SizeBytes)
            .Take(MaxChildren)
            .ToList();

        return (total, children);
    }

    /// <summary>
    /// Распознать известную папку профиля, чтобы подписать её простыми словами. Сначала — по реальным
    /// путям (учитывает перенаправление папок), затем по имени папки (для Загрузок/AppData/OneDrive,
    /// у которых нет своего <see cref="Environment.SpecialFolder"/>).
    /// </summary>
    private static UserFolderKind ClassifyFolder(string path)
    {
        // Корень папки пользователя (C:\Users\Имя) — здесь ВСЕ личные файлы. Проверяем первым.
        if (PathEquals(path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            return UserFolderKind.UserProfile;
        }

        if (PathEquals(path, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)))
        {
            return UserFolderKind.Desktop;
        }

        if (PathEquals(path, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)))
        {
            return UserFolderKind.Documents;
        }

        if (PathEquals(path, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)))
        {
            return UserFolderKind.Pictures;
        }

        if (PathEquals(path, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)))
        {
            return UserFolderKind.Music;
        }

        if (PathEquals(path, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)))
        {
            return UserFolderKind.Videos;
        }

        var leaf = Path.GetFileName(path.TrimEnd('\\', '/'));
        return leaf.ToLowerInvariant() switch
        {
            "downloads" or "загрузки" => UserFolderKind.Downloads,
            "desktop" or "рабочий стол" => UserFolderKind.Desktop,
            "documents" or "документы" => UserFolderKind.Documents,
            "pictures" or "изображения" => UserFolderKind.Pictures,
            "music" or "музыка" => UserFolderKind.Music,
            "videos" or "видео" => UserFolderKind.Videos,
            "appdata" => UserFolderKind.AppData,
            "onedrive" => UserFolderKind.OneDrive,
            _ => UserFolderKind.Other,
        };
    }

    private static bool PathEquals(string a, string? b) =>
        !string.IsNullOrEmpty(b) &&
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static long DirectorySize(string path, CancellationToken cancellationToken)
    {
        long total = 0;
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            // FileInfo из DirectoryInfo.EnumerateFiles несёт готовый размер — без лишнего syscall на файл.
            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    total += file.Length;
                }
                catch (Exception)
                {
                    // Файл исчез/занят — пропускаем.
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Папка недоступна — то, что успели, вернём.
        }

        return total;
    }
}
