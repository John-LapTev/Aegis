using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;
using Microsoft.Win32;

namespace Aegis.System.Fixing;

/// <summary>
/// Ищет и удаляет остатки после удаления программы (в духе Revo). Работает для ЛЮБОЙ программы без «наблюдения» —
/// по имени и ПО ПУТИ к папке установки: папка установки; папки в AppData Local/Roaming/LocalLow/ProgramData; ветки
/// настроек в SOFTWARE; осиротевшая ветка «Uninstall»; ярлыки (Пуск/Рабочий стол) и записи автозапуска (Run/RunOnce),
/// ведущие в папку программы; плюс записанный «след установки» (если ставили с наблюдением). Поиск НИЧЕГО не удаляет.
/// Удаление — только по выбору пользователя, с предохранителем PathSafety: файлы/папки-остатки удаляются НАСОВСЕМ
/// (по выбору Ивана — не в Корзину, правка 1204), записи реестра — с бэкапом (обратимы).
/// </summary>
public sealed class LeftoverService : ILeftoverService
{
    private const long MaxSizeScanBytes = 5L * 1024 * 1024 * 1024; // предохранитель на подсчёт размера папки

    private readonly IInstallTraceStore _traceStore;
    private readonly RegistryKeyBackupStore _regBackup;

    public LeftoverService(IInstallTraceStore traceStore, RegistryKeyBackupStore regBackup)
    {
        ArgumentNullException.ThrowIfNull(traceStore);
        ArgumentNullException.ThrowIfNull(regBackup);
        _traceStore = traceStore;
        _regBackup = regBackup;
    }

    public Task<IReadOnlyList<LeftoverItem>> ScanAsync(InstalledProgram program, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<LeftoverItem>>(() => Scan(program, cancellationToken), cancellationToken);

    private IReadOnlyList<LeftoverItem> Scan(InstalledProgram program, CancellationToken cancellationToken)
    {
        var items = new List<LeftoverItem>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddFolder(string? path)
        {
            // PathSafety не даёт предложить к удалению корни, системные и ОБЩИЕ папки вендоров (Microsoft/Google…).
            if (string.IsNullOrWhiteSpace(path) || !PathSafety.IsSafeToDeleteFolder(path)
                || !Directory.Exists(path) || !seenPaths.Add(path))
            {
                return;
            }

            items.Add(new LeftoverItem
            {
                Kind = LeftoverKind.Folder,
                Path = path,
                Display = path,
                SizeBytes = FolderSize(path, cancellationToken),
            });
        }

        try
        {
            // 1. Папка установки, если осталась после штатного удаления.
            AddFolder(program.InstallLocation);

            // 2. Папки в профиле пользователя по имени программы / имени папки установки (Local, Roaming, LocalLow, ProgramData).
            var localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");
            foreach (var name in CandidateNames(program))
            {
                AddFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name));
                AddFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), name));
                AddFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), name));
                AddFolder(Path.Combine(localLow, name));
            }

            // 3. Записанный «след установки» (если ставили с наблюдением) — точные добавленные пути и ветки реестра.
            var trace = _traceStore.Find(program.Name);
            if (trace is not null)
            {
                foreach (var path in trace.AddedPaths)
                {
                    if (Directory.Exists(path))
                    {
                        AddFolder(path);
                    }
                    else if (File.Exists(path) && seenPaths.Add(path))
                    {
                        items.Add(new LeftoverItem { Kind = LeftoverKind.File, Path = path, Display = path, SizeBytes = FileSize(path) });
                    }
                }

                foreach (var key in trace.AddedRegistryKeys)
                {
                    AddRegistry(items, seenPaths, key);
                }
            }

            // 4. Осиротевшая ветка «Uninstall» — если деинсталлятор её не убрал.
            var regPath = ProgramUninstaller.ToRegExePath(program.RegistryKeyPath);
            if (regPath is not null && RegistryKeyExists(regPath))
            {
                AddRegistry(items, seenPaths, regPath);
            }

            // 5. Ветки настроек программы в SOFTWARE (по имени папки установки и названию) — типичный «мусор в реестре»
            //    после удаления. Всё под предохранителем IsSafeRegistryKey (не трогаем общие ветки).
            var softwareRoots = new[] { @"HKCU\SOFTWARE", @"HKLM\SOFTWARE", @"HKLM\SOFTWARE\WOW6432Node" };
            var nameCandidates = RegistryCandidates(program).ToList();
            foreach (var name in nameCandidates)
            {
                foreach (var root in softwareRoots)
                {
                    var key = $@"{root}\{name}";
                    if (RegistryKeyExists(key))
                    {
                        AddRegistry(items, seenPaths, key);
                    }
                }
            }

            // 5b. Ветку издателя (напр. SOFTWARE\Piriform) НЕ предлагаем целиком — она общая для нескольких продуктов
            //     вендора (снесли бы настройки соседних программ, аудит 2026-07-03). Берём только КОНКРЕТНУЮ подветку
            //     продукта под издателем: SOFTWARE\<Издатель>\<НазваниеПродукта>.
            var publisher = program.Publisher?.Trim();
            if (!string.IsNullOrWhiteSpace(publisher) && publisher.Length > 3)
            {
                foreach (var name in nameCandidates)
                {
                    foreach (var root in softwareRoots)
                    {
                        var key = $@"{root}\{publisher}\{name}";
                        if (RegistryKeyExists(key))
                        {
                            AddRegistry(items, seenPaths, key);
                        }
                    }
                }
            }

            // Дальше — поиск ПО ПУТИ к папке программы: работает для ЛЮБОЙ программы без всякого «наблюдения» (это главное,
            // что нужно — чистить хвосты старых установленных программ). Точно и безопасно: берём только то, что реально
            // указывает В папку установки.
            var installRoot = InstallRoot(program.InstallLocation);
            if (installRoot is not null)
            {
                // 6. Ярлыки (меню Пуск + Рабочий стол), ведущие в папку программы — после удаления остаются битыми.
                foreach (var shortcut in ShortcutsPointingInto(installRoot, cancellationToken))
                {
                    if (seenPaths.Add(shortcut))
                    {
                        items.Add(new LeftoverItem { Kind = LeftoverKind.File, Path = shortcut, Display = shortcut, SizeBytes = FileSize(shortcut) });
                    }
                }
            }

            // 7. Записи автозапуска (Run/RunOnce): по пути в папку программы ИЛИ по её имени. ВАЖНО: ищем всегда, даже
            //    если папки уже нет (иначе после удаления папки запись автозапуска оставалась висеть и программа
            //    «возвращалась» в список автозапуска — баг Ивана 1323).
            AddStartupLeftovers(items, seenPaths, program, installRoot);
        }
        catch (Exception)
        {
            // best-effort: возвращаем то, что успели найти
        }

        return items;
    }

    public Task<int> RemoveAsync(IReadOnlyList<LeftoverItem> items, CancellationToken cancellationToken = default) =>
        Task.Run(() => Remove(items), cancellationToken);

    private int Remove(IReadOnlyList<LeftoverItem> items)
    {
        var removed = 0;
        foreach (var item in items)
        {
            try
            {
                switch (item.Kind)
                {
                    // Остатки удалённой ПРОГРАММЫ удаляем НАСОВСЕМ (не в Корзину) — так надёжно уходят и большие папки,
                    // которые в Корзину не влезают (правка Ивана 1204). Корзина остаётся для «Удалить файл» на Дашборде.
                    // Защита в глубину: ПЕРЕД безвозвратным удалением ещё раз сверяем путь с PathSafety (не только на этапе
                    // Scan) — чтобы никакой источник элементов не мог провести системную/общую папку (аудит 2026-07-04).
                    case LeftoverKind.Folder when Directory.Exists(item.Path) && PathSafety.IsSafeToDeleteFolder(item.Path):
                        Directory.Delete(item.Path, recursive: true);
                        removed++;
                        break;
                    case LeftoverKind.File when File.Exists(item.Path):
                        File.Delete(item.Path);
                        removed++;
                        break;
                    case LeftoverKind.RegistryValue:
                        if (RemoveRegistryValue(item.Path, item.ValueName))
                        {
                            removed++;
                        }

                        break;
                    case LeftoverKind.RegistryKey:
                        if (RemoveRegistry(item.Path))
                        {
                            removed++;
                        }

                        break;
                }
            }
            catch (Exception)
            {
                // один остаток не поддался — продолжаем с остальными
            }
        }

        return removed;
    }

    private static void AddRegistry(List<LeftoverItem> items, HashSet<string> seen, string regPath)
    {
        // Предохранитель: не предлагаем к удалению системные/общие ветки реестра (Microsoft/Google/сам SOFTWARE и т.п.).
        if (PathSafety.IsSafeRegistryKey(regPath) && seen.Add(regPath))
        {
            items.Add(new LeftoverItem { Kind = LeftoverKind.RegistryKey, Path = regPath, Display = regPath });
        }
    }

    /// <summary>
    /// Имена-кандидаты для веток реестра: папка установки и полное название программы (конкретные). Издатель сюда НЕ
    /// входит — его ветка общая для нескольких продуктов; издатель используется только как префикс подветки продукта.
    /// </summary>
    private static IEnumerable<string> RegistryCandidates(InstalledProgram program) =>
        new HashSet<string>(CandidateNames(program), StringComparer.OrdinalIgnoreCase);

    /// <summary>Нормализованный корень папки установки (с «\» на конце) для точного сравнения путей; null — если пусто/широко/небезопасно.</summary>
    private static string? InstallRoot(string? installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return null;
        }

        var root = installLocation.Replace('/', '\\').TrimEnd('\\');
        // Слишком широкий корень (C:\, Program Files) как якорь не берём — иначе «в папке программы» совпадёт со всем.
        return root.Length > 3 && PathSafety.IsSafeToDeleteFolder(root) ? root + "\\" : null;
    }

    /// <summary>Ярлыки (меню Пуск + Рабочий стол, все пользователи и текущий), цель которых — внутри папки программы.</summary>
    private static IEnumerable<string> ShortcutsPointingInto(string installRoot, CancellationToken cancellationToken)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        };

        foreach (var dir in roots)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                continue;
            }

            IEnumerable<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var shortcut in shortcuts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? target;
                try
                {
                    target = ShortcutResolver.ResolveTarget(shortcut);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(target)
                    && target.Replace('/', '\\').StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
                {
                    yield return shortcut;
                }
            }
        }
    }

    /// <summary>Записи автозапуска (Run/RunOnce, HKCU+HKLM), команда которых указывает в папку программы.</summary>
    private static void AddStartupLeftovers(List<LeftoverItem> items, HashSet<string> seen, InstalledProgram program, string? installRoot)
    {
        var names = CandidateNames(program).Where(n => n.Length >= 3).ToList();
        (RegistryKey Hive, string Prefix)[] hives = [(Registry.CurrentUser, "HKCU"), (Registry.LocalMachine, "HKLM")];
        string[] subs =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        ];

        foreach (var (hive, prefix) in hives)
        {
            foreach (var sub in subs)
            {
                try
                {
                    using var key = hive.OpenSubKey(sub);
                    if (key is null)
                    {
                        continue;
                    }

                    foreach (var valueName in key.GetValueNames())
                    {
                        var data = key.GetValue(valueName) as string ?? string.Empty;
                        // Совпадение по ПАПКЕ (команда указывает в папку программы) ИЛИ по ИМЕНИ (имя значения/команда
                        // содержат название программы как отдельное слово) — чтобы поймать запись и после удаления папки.
                        var byFolder = installRoot is not null && !string.IsNullOrWhiteSpace(data)
                            && data.Replace('/', '\\').IndexOf(installRoot, StringComparison.OrdinalIgnoreCase) >= 0;
                        var byName = names.Any(n => NameMatch.ReferencesName(valueName, n) || NameMatch.ReferencesName(data, n));
                        if (!byFolder && !byName)
                        {
                            continue;
                        }

                        var keyPath = $@"{prefix}\{sub}";
                        if (seen.Add(keyPath + "|" + valueName))
                        {
                            items.Add(new LeftoverItem
                            {
                                Kind = LeftoverKind.RegistryValue,
                                Path = keyPath,
                                ValueName = valueName,
                                Display = $"Автозапуск: {valueName}",
                            });
                        }
                    }
                }
                catch (Exception)
                {
                    // недоступная ветка — пропускаем
                }
            }
        }
    }

    /// <summary>
    /// Имена-кандидаты для поиска остаточных папок. Берём ТОЛЬКО имя папки установки (оно конкретное, напр. «OperaGX»)
    /// и полное название программы. НЕ используем «первое слово имени» — оно даёт общие папки вендоров («Microsoft»,
    /// «Google»), а их удалять нельзя (плюс PathSafety это дополнительно блокирует).
    /// </summary>
    private static IEnumerable<string> CandidateNames(InstalledProgram program)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(program.InstallLocation))
        {
            var leaf = new DirectoryInfo(program.InstallLocation.TrimEnd('\\', '/')).Name;
            if (leaf.Length > 2)
            {
                names.Add(leaf);
            }
        }

        // Полное название целиком (конкретное) — на случай, если папка названа как программа.
        var full = program.Name.Trim();
        if (full.Length > 3)
        {
            names.Add(full);
        }

        return names;
    }

    private static long FileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static long FolderSize(string path, CancellationToken cancellationToken)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += FileSize(file);
                if (total >= MaxSizeScanBytes)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
            // нет доступа к части папки — вернём, что успели посчитать
        }

        return total;
    }

    private static bool RegistryKeyExists(string regPath) =>
        ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"query \"{regPath}\"");

    /// <summary>Удаляет ОДНО значение автозапуска в ветке Run/RunOnce (не саму ветку). Бэкап ветки перед удалением.</summary>
    private bool RemoveRegistryValue(string keyPath, string? valueName)
    {
        if (string.IsNullOrWhiteSpace(valueName))
        {
            return false;
        }

        // Бэкап всей ветки Run перед удалением значения (обратимость). Удаляем только одно значение — саму ветку не трогаем.
        if (_regBackup.Backup(keyPath, "Остаток удаления программы (автозапуск)") is null)
        {
            return false;
        }

        return ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"delete \"{keyPath}\" /v \"{valueName}\" /f");
    }

    private bool RemoveRegistry(string regPath)
    {
        // Предохранитель и при удалении (не только при показе): системные/общие ветки не трогаем.
        if (!PathSafety.IsSafeRegistryKey(regPath) || !RegistryKeyExists(regPath))
        {
            return false;
        }

        if (_regBackup.Backup(regPath, "Остаток удаления программы") is null)
        {
            return false; // без бэкапа не удаляем (обратимость)
        }

        return ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"delete \"{regPath}\" /f");
    }
}
