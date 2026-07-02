using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник «остатков»: смотрит папки в %AppData% (Roaming) и %LocalAppData% и определяет, какие
/// из них ПУСТЫЕ (надёжный признак остатка). Имя сверяет со списком установленных программ; системные/dev/
/// cloud-папки в чёрном списке не трогает. Только читает; ничего не удаляет.
/// </summary>
public sealed class LeftoverProbe : ILeftoverProbe
{
    // Служебные/системные/dev/cloud-папки — НИКОГДА не предлагать как остаток (даже если пусты).
    private static readonly HashSet<string> Keep = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows/служебные
        "Microsoft", "Packages", "Temp", "TempState", "Comms", "ConnectedDevicesPlatform", "CrashDumps",
        "D3DSCache", "Programs", "VirtualStore", "Publishers", "GroupPolicy", "ElevatedDiagnostics",
        "Diagnostics", "INetCache", "INetCookies", "IconCache", "History", "PeerDistRepub", "AppV",
        "Application Data", "PlaceholderTileLogoFolder",
        // dev-инструменты (кэши/конфиги без записи в Uninstall)
        "npm", "npm-cache", "pip", "yarn", "pnpm", "Composer", "gradle", ".gradle", "nvm", "NuGet",
        "JetBrains", "Code", "GitHubDesktop", "pip-cache", "go", "Cargo",
        // облачные/синк
        "OneDrive", "Dropbox", "Google", "Yandex", "Mozilla",
    };

    public Task<LeftoverSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var installed = InstalledPrograms.Read(includePublisher: true);
        var folders = new List<LeftoverFolder>();

        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 })
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name) || Keep.Contains(name))
                    {
                        continue;
                    }

                    // Нужна только пустота папки (надёжный сигнал остатка) — размер/давность не считаем
                    // (это сэкономило бы рекурсивный обход гигабайтов в %AppData% и не нужно для пустых).
                    folders.Add(new LeftoverFolder
                    {
                        Name = name,
                        Path = dir,
                        SizeBytes = 0,
                        IsEmpty = IsTrulyEmpty(dir),
                        MatchesInstalled = MatchesInstalled(name, installed),
                        RecentlyUsed = false,
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Корень недоступен — пропускаем.
            }
        }

        return Task.FromResult(new LeftoverSnapshot { Folders = folders });
    }

    // Папка ПУСТА только если в ней гарантированно нет ни файлов, ни подпапок. Если обойти не удалось
    // (нет прав/занята) — считаем НЕ пустой (безопаснее: не предложим удалить как «пустую»).
    private static bool IsTrulyEmpty(string dir)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(dir).Any();
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Имя папки относится к установленной программе, если встречается в названии/издателе любой из них
    // (или наоборот). Короткие имена (<4) не матчим по подстроке — считаем «совпало» (не рискуем флагать).
    private static bool MatchesInstalled(string folderName, IReadOnlyList<string> installed)
    {
        if (folderName.Length < 4)
        {
            return true;
        }

        foreach (var program in installed)
        {
            if (program.Contains(folderName, StringComparison.OrdinalIgnoreCase)
                || folderName.Contains(program, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
