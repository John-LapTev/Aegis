using Aegis.Scanners.Probing;
using Aegis.System.Internal;
using Microsoft.Win32;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник остатков игр Steam: находит Steam и его библиотеки (libraryfolders.vdf), список
/// реально установленных игр (appmanifest_*.acf) и хвосты удалённых — кэши шейдеров без установленной игры
/// и папки-следы пиратских копий. Только читает; ничего не удаляет.
/// </summary>
public sealed class SteamLeftoverProbe : ISteamLeftoverProbe
{
    // Папки-следы пираток (относительно корня профиля). Если существуют и непусты — кандидаты-остатки.
    private static readonly (Environment.SpecialFolder Root, string Relative)[] CrackFolders =
    [
        (Environment.SpecialFolder.ApplicationData, @"Steam\CODEX"),
        (Environment.SpecialFolder.ApplicationData, @"Steam\RUNE"),
        (Environment.SpecialFolder.ApplicationData, @"Steam\STEAMPUNKS"),
        (Environment.SpecialFolder.ApplicationData, "Goldberg SteamEmu Saves"),
        (Environment.SpecialFolder.ApplicationData, "GSE Saves"),
        (Environment.SpecialFolder.ApplicationData, "SmartSteamEmu"),
        (Environment.SpecialFolder.ApplicationData, "EMPRESS"),
        (Environment.SpecialFolder.ApplicationData, "OnlineFix"),
        (Environment.SpecialFolder.ApplicationData, "ALI213"),
        (Environment.SpecialFolder.MyDocuments, @"Steam\CODEX"),
        (Environment.SpecialFolder.MyDocuments, @"Steam\RUNE"),
        (Environment.SpecialFolder.CommonDocuments, @"Steam\CODEX"),
        (Environment.SpecialFolder.CommonDocuments, @"Steam\RUNE"),
        (Environment.SpecialFolder.CommonDocuments, "OnlineFix"),
    ];

    public Task<SteamLeftoverSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<SteamLeftover>();

        var steamPath = FindSteamPath();
        if (steamPath is not null)
        {
            var libraries = FindLibraries(steamPath);
            var installed = InstalledAppIds(libraries, cancellationToken);
            AddOrphanCaches(libraries, installed, items, cancellationToken);
        }

        AddCrackResidue(items, cancellationToken);

        return Task.FromResult(new SteamLeftoverSnapshot { Items = items });
    }

    private static string? FindSteamPath()
    {
        var path = RegistryReader.GetString(RegistryHive.CurrentUser, @"Software\Valve\Steam", "SteamPath")
                   ?? RegistryReader.GetString(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
                   ?? RegistryReader.GetString(RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        path = path.Replace('/', '\\');
        return Directory.Exists(path) ? path : null;
    }

    // Корни всех библиотек Steam (включая основную) — из libraryfolders.vdf + сам Steam.
    private static IReadOnlyList<string> FindLibraries(string steamPath)
    {
        var libraries = new List<string> { steamPath };
        foreach (var vdf in new[]
                 {
                     Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"),
                     Path.Combine(steamPath, "config", "libraryfolders.vdf"),
                 })
        {
            try
            {
                if (File.Exists(vdf))
                {
                    foreach (var libPath in SteamVdf.ParseLibraryPaths(File.ReadAllText(vdf)))
                    {
                        if (Directory.Exists(libPath) && !libraries.Contains(libPath, StringComparer.OrdinalIgnoreCase))
                        {
                            libraries.Add(libPath);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Файл недоступен — пропускаем.
            }
        }

        return libraries;
    }

    private static HashSet<string> InstalledAppIds(IReadOnlyList<string> libraries, CancellationToken cancellationToken)
    {
        var installed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamApps = Path.Combine(library, "steamapps");
            try
            {
                if (!Directory.Exists(steamApps))
                {
                    continue;
                }

                foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
                {
                    var appId = SteamVdf.AppIdFromManifest(Path.GetFileName(manifest));
                    if (appId is not null)
                    {
                        installed.Add(appId);
                    }
                }
            }
            catch (Exception)
            {
                // Библиотека недоступна — пропускаем.
            }
        }

        return installed;
    }

    // Папки кэша шейдеров под AppID, у которого нет установленной игры — кэш удалённой игры.
    private static void AddOrphanCaches(
        IReadOnlyList<string> libraries, HashSet<string> installed, List<SteamLeftover> items, CancellationToken cancellationToken)
    {
        foreach (var library in libraries)
        {
            var shaderCache = Path.Combine(library, "steamapps", "shadercache");
            try
            {
                if (!Directory.Exists(shaderCache))
                {
                    continue;
                }

                foreach (var dir in Directory.EnumerateDirectories(shaderCache))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var appId = Path.GetFileName(dir);
                    // Папки кэша названы числовым AppID. Если игра не установлена — это остаток.
                    if (appId.Length > 0 && appId.All(char.IsDigit) && !installed.Contains(appId))
                    {
                        items.Add(new SteamLeftover
                        {
                            Title = $"Кэш удалённой игры (Steam, AppID {appId})",
                            Path = dir,
                            Kind = SteamLeftoverKind.OrphanCache,
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Кэш недоступен — пропускаем.
            }
        }
    }

    private static void AddCrackResidue(List<SteamLeftover> items, CancellationToken cancellationToken)
    {
        foreach (var (root, relative) in CrackFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rootPath = Environment.GetFolderPath(root);
            if (string.IsNullOrEmpty(rootPath))
            {
                continue;
            }

            var full = Path.Combine(rootPath, relative);
            try
            {
                if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
                {
                    items.Add(new SteamLeftover
                    {
                        Title = $"Возможные остатки пиратской игры: {relative}",
                        Path = full,
                        Kind = SteamLeftoverKind.CrackResidue,
                    });
                }
            }
            catch (Exception)
            {
                // Папка недоступна — пропускаем.
            }
        }
    }
}
