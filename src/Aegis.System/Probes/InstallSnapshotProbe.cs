using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Microsoft.Win32;

namespace Aegis.System.Probes;

/// <summary>
/// Снимает состояние наблюдаемых мест, куда программы обычно ставятся: папки (Program Files, ProgramData, AppData),
/// ярлыки в меню «Пуск» и ветки реестра (SOFTWARE + списки «Установка/удаление»). Сравнение снимков ДО и ПОСЛЕ
/// установки даёт «след» программы. Всё best-effort: недоступный источник просто пропускается. Только Windows.
/// </summary>
public sealed class InstallSnapshotProbe : IInstallSnapshotProbe
{
    private const int MaxPaths = 40000; // предохранитель, чтобы снимок не разросся на нетипичных системах
    private const int DirDepth = 2;      // папки установки на 2 уровня вглубь — этого хватает опознать программу

    public Task<InstallSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Capture(cancellationToken), cancellationToken);

    private static InstallSnapshot Capture(CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        var keys = new List<string>();

        try
        {
            CapturePaths(paths, cancellationToken);
            CaptureRegistry(keys);
        }
        catch (Exception)
        {
            // best-effort: что успели собрать, то и вернём
        }

        return new InstallSnapshot { Paths = paths, RegistryKeys = keys };
    }

    private static void CapturePaths(List<string> paths, CancellationToken cancellationToken)
    {
        foreach (var root in FolderRoots())
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                continue;
            }

            CollectDirectories(root, DirDepth, paths, cancellationToken);
        }

        foreach (var menu in StartMenuRoots())
        {
            if (string.IsNullOrEmpty(menu) || !Directory.Exists(menu))
            {
                continue;
            }

            try
            {
                foreach (var shortcut in Directory.EnumerateFiles(menu, "*.lnk", SearchOption.AllDirectories))
                {
                    paths.Add(shortcut);
                    if (paths.Count >= MaxPaths)
                    {
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // недоступная папка меню — пропускаем
            }
        }
    }

    private static void CollectDirectories(string root, int depth, List<string> paths, CancellationToken cancellationToken)
    {
        if (depth < 0 || paths.Count >= MaxPaths)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> subDirectories;
        try
        {
            subDirectories = Directory.EnumerateDirectories(root);
        }
        catch (Exception)
        {
            return; // нет доступа к папке — пропускаем ветку
        }

        foreach (var directory in subDirectories)
        {
            paths.Add(directory);
            if (paths.Count >= MaxPaths)
            {
                return;
            }

            CollectDirectories(directory, depth - 1, paths, cancellationToken);
        }
    }

    private static void CaptureRegistry(List<string> keys)
    {
        AddSubKeys(Registry.LocalMachine, @"SOFTWARE", "HKLM", keys);
        AddSubKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node", "HKLM", keys);
        AddSubKeys(Registry.CurrentUser, @"SOFTWARE", "HKCU", keys);
        AddSubKeys(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM", keys);
        AddSubKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM", keys);
        AddSubKeys(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU", keys);
    }

    private static void AddSubKeys(RegistryKey hive, string path, string hivePrefix, List<string> keys)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            if (key is null)
            {
                return;
            }

            foreach (var name in key.GetSubKeyNames())
            {
                keys.Add($@"{hivePrefix}\{path}\{name}");
            }
        }
        catch (Exception)
        {
            // недоступная ветка — пропускаем
        }
    }

    private static IEnumerable<string> FolderRoots() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), // ProgramData
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),  // AppData\Local
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),       // AppData\Roaming
    ];

    private static IEnumerable<string> StartMenuRoots() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
    ];
}
