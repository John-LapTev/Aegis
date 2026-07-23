using Microsoft.Win32;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник контекстного меню проводника: обходит ветки реестра, где живут пункты правого клика, и
/// находит те, что ведут в никуда (программа удалена). Только читает.
///
/// Отключение таких пунктов делается штатными средствами Windows и полностью обратимо:
/// • команда — значением <c>LegacyDisable</c> в её ключе;
/// • обработчик-расширение — записью его CLSID в системный список заблокированных расширений оболочки.
/// Ничего не удаляется, поэтому «Вернуть» восстанавливает пункт полностью.
/// </summary>
public sealed class ContextMenuProbe : IContextMenuProbe
{
    /// <summary>Системный список заблокированных расширений оболочки — штатный механизм Windows.</summary>
    private const string BlockedExtensionsKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

    /// <summary>Где искать пункты меню: путь в реестре → как объяснить человеку, где этот пункт виден.</summary>
    private static readonly (string Root, string Scope)[] Locations =
    [
        (@"*\shell", "на любых файлах"),
        (@"Directory\shell", "на папках"),
        (@"Directory\Background\shell", "на пустом месте внутри папки"),
        (@"Folder\shell", "на папках"),
        (@"Drive\shell", "на дисках"),
        (@"AllFilesystemObjects\shell", "на файлах и папках"),
    ];

    /// <summary>Обработчики-расширения (загружаемые библиотеки) — самая частая причина медленного правого клика.</summary>
    private static readonly (string Root, string Scope)[] HandlerLocations =
    [
        (@"*\shellex\ContextMenuHandlers", "на любых файлах"),
        (@"Directory\shellex\ContextMenuHandlers", "на папках"),
        (@"Directory\Background\shellex\ContextMenuHandlers", "на пустом месте внутри папки"),
        (@"Folder\shellex\ContextMenuHandlers", "на папках"),
        (@"Drive\shellex\ContextMenuHandlers", "на дисках"),
        (@"AllFilesystemObjects\shellex\ContextMenuHandlers", "на файлах и папках"),
    ];

    public Task<IReadOnlyList<ContextMenuEntry>> ReadBrokenAsync(CancellationToken cancellationToken = default)
    {
        var broken = new List<ContextMenuEntry>();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<ContextMenuEntry>>(broken);
        }

        var blocked = ReadBlockedExtensions();

        foreach (var (root, scope) in Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectCommands(root, scope, broken);
        }

        foreach (var (root, scope) in HandlerLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectHandlers(root, scope, blocked, broken);
        }

        return Task.FromResult<IReadOnlyList<ContextMenuEntry>>(broken);
    }

    /// <summary>Пункты-команды: у каждого есть подключ <c>command</c> со строкой запуска.</summary>
    private static void CollectCommands(string root, string scope, List<ContextMenuEntry> into)
    {
        try
        {
            using var shell = Registry.ClassesRoot.OpenSubKey(root);
            foreach (var name in shell?.GetSubKeyNames() ?? [])
            {
                // Имя с «-» в начале означает «уже отключено» — повторно предлагать нечего.
                if (name.StartsWith('-'))
                {
                    continue;
                }

                using var entry = shell!.OpenSubKey(name);
                if (entry is null || entry.GetValue("LegacyDisable") is not null)
                {
                    continue;
                }

                using var command = entry.OpenSubKey("command");
                var line = command?.GetValue(null)?.ToString();
                if (!ContextMenuAnalyzer.IsBroken(line, File.Exists))
                {
                    continue;
                }

                into.Add(new ContextMenuEntry
                {
                    Name = ContextMenuAnalyzer.DisplayName(name, entry.GetValue(null)?.ToString()),
                    Scope = scope,
                    Target = ContextMenuAnalyzer.ExtractExecutablePath(line),
                    Hive = "HKCR",
                    SubKey = $@"{root}\{name}",
                    ValueName = "LegacyDisable",
                });
            }
        }
        catch (Exception)
        {
            // Ветка недоступна — пропускаем (best-effort).
        }
    }

    /// <summary>Обработчики-расширения: значение по умолчанию — CLSID библиотеки, которая грузится в проводник.</summary>
    private static void CollectHandlers(string root, string scope, IReadOnlySet<string> blocked, List<ContextMenuEntry> into)
    {
        try
        {
            using var handlers = Registry.ClassesRoot.OpenSubKey(root);
            foreach (var name in handlers?.GetSubKeyNames() ?? [])
            {
                if (name.StartsWith('-'))
                {
                    continue;
                }

                using var handler = handlers!.OpenSubKey(name);
                var clsid = (handler?.GetValue(null)?.ToString() ?? name).Trim();
                if (!clsid.StartsWith('{') || blocked.Contains(clsid))
                {
                    continue;
                }

                var library = ResolveHandlerLibrary(clsid);
                if (library is null || File.Exists(Environment.ExpandEnvironmentVariables(library)))
                {
                    continue; // библиотека на месте — пункт рабочий
                }

                into.Add(new ContextMenuEntry
                {
                    Name = ContextMenuAnalyzer.DisplayName(name, null),
                    Scope = scope,
                    Target = library,
                    Hive = "HKLM",
                    SubKey = BlockedExtensionsKey,
                    ValueName = clsid,
                });
            }
        }
        catch (Exception)
        {
            // Ветка недоступна — пропускаем.
        }
    }

    /// <summary>Путь к библиотеке обработчика по его CLSID (null — определить не удалось).</summary>
    private static string? ResolveHandlerLibrary(string clsid)
    {
        try
        {
            using var server = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}\InprocServer32");
            var path = server?.GetValue(null)?.ToString()?.Trim().Trim('"');
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Уже заблокированные расширения оболочки — их предлагать не нужно.</summary>
    private static IReadOnlySet<string> ReadBlockedExtensions()
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(BlockedExtensionsKey);
            foreach (var name in key?.GetValueNames() ?? [])
            {
                blocked.Add(name);
            }
        }
        catch (Exception)
        {
            // Ключа может не быть — значит ничего не заблокировано.
        }

        return blocked;
    }
}
