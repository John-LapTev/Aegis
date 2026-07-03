using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Удаляет программу штатным деинсталлятором (тихим, если есть) и дочищает остатки: пустую папку установки —
/// в Корзину (обратимо), осиротевшую ветку реестра «Uninstall» — с бэкапом ветки перед удалением. Windows-слой.
/// </summary>
public sealed class ProgramUninstaller : IProgramUninstaller
{
    private const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Wow6432Root = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly RegistryKeyBackupStore _regBackup;
    private readonly IInstallTraceStore _traceStore;

    public ProgramUninstaller(RegistryKeyBackupStore regBackup, IInstallTraceStore traceStore)
    {
        ArgumentNullException.ThrowIfNull(regBackup);
        ArgumentNullException.ThrowIfNull(traceStore);
        _regBackup = regBackup;
        _traceStore = traceStore;
    }

    public async Task<UninstallResult> UninstallAsync(
        InstalledProgram program, bool cleanLeftovers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);

        var command = program.QuietUninstallCommand ?? program.UninstallCommand;
        if (string.IsNullOrWhiteSpace(command))
        {
            return UninstallResult.Failed("У этой программы нет команды удаления — удалите её через «Параметры» Windows.");
        }

        var (exe, args) = ParseCommand(command);
        if (string.IsNullOrWhiteSpace(exe))
        {
            return UninstallResult.Failed("Не удалось разобрать команду удаления этой программы.");
        }

        int code;
        try
        {
            // Деинсталлятор может показать своё окно и ждать пользователя — это нормально.
            code = await ProcessRunner.RunAsync(exe, args, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UninstallResult.Failed("Не удалось запустить удаление: " + ex.Message);
        }

        // 0 — успех; 3010 — успех, нужна перезагрузка; 1602 — пользователь отменил в окне деинсталлятора.
        if (code == 1602)
        {
            return UninstallResult.Failed("Удаление отменено в окне деинсталлятора.");
        }

        if (code != 0 && code != 3010)
        {
            return UninstallResult.Failed($"Деинсталлятор завершился с кодом {code} — программа могла не удалиться.");
        }

        var removedFolder = false;
        var removedKey = false;
        var tracedRemoved = 0;
        if (cleanLeftovers)
        {
            removedFolder = TryRemoveEmptyInstallFolder(program.InstallLocation);
            removedKey = TryRemoveOrphanRegistryKey(program.RegistryKeyPath, program.Name);
            // Если при установке велось наблюдение — вычищаем ВСЁ, что добавил установщик (обратимо: Корзина + бэкап реестра).
            tracedRemoved = TryRemoveTracedLeftovers(program.Name);
        }

        var extra = (removedFolder, removedKey) switch
        {
            (true, true) => " Дочищены остатки: пустая папка (в Корзину) и запись в реестре.",
            (true, false) => " Дочищена пустая папка (в Корзину).",
            (false, true) => " Дочищена осиротевшая запись в реестре.",
            _ => string.Empty,
        };

        if (tracedRemoved > 0)
        {
            extra += $" Полное удаление: по записанному следу установки вычищено ещё {tracedRemoved} " +
                     "остатков программы (файлы — в Корзину, ветки реестра — с бэкапом).";
        }

        // Проверка ФАКТА удаления: некоторые деинсталляторы (особенно у игр/лаунчеров) возвращают код 0, но программу
        // не убирают. Если её запись «Uninstall» всё ещё на месте — считаем, что штатно НЕ удалилось (нужно до-удаление).
        // ВАЖНО: часть деинсталляторов (InnoSetup/NSIS) перезапускается из temp и завершает родительский процесс ДО
        // фактического удаления — поэтому опрашиваем ветку несколько раз с паузой, прежде чем делать вывод «не удалилось»
        // (иначе ложная тревога и преждевременное до-удаление ещё живой папки — аудит 2026-07-03).
        var stillRegistered = false;
        var uninstallRegPath = ToRegExePath(program.RegistryKeyPath);
        if (uninstallRegPath is not null)
        {
            stillRegistered = true;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }

                if (!ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"query \"{uninstallRegPath}\""))
                {
                    stillRegistered = false;
                    break;
                }
            }
        }

        return new UninstallResult
        {
            Success = true,
            StillRegistered = stillRegistered,
            Message = stillRegistered ? "Штатный деинсталлятор не убрал программу полностью." : "Программа удалена." + extra,
            RemovedLeftoverFolder = removedFolder,
            RemovedOrphanRegistryKey = removedKey,
        };
    }

    /// <summary>Разобрать UninstallString на исполняемый файл и аргументы (учитывает кавычки вокруг пути).</summary>
    public static (string Exe, string Args) ParseCommand(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 0)
            {
                return (command[1..end], command[(end + 1)..].Trim());
            }
        }

        // Незакавыченный путь с пробелами (напр. «C:\Program Files\App\uninst.exe /S»): режем по границе «.exe»,
        // а не по первому пробелу — иначе получим «C:\Program» и запуск сорвётся.
        var exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            var end = exeIndex + 4;
            return (command[..end], command[end..].Trim());
        }

        var space = command.IndexOf(' ');
        return space < 0 ? (command, string.Empty) : (command[..space], command[(space + 1)..].Trim());
    }

    /// <summary>Путь ветки для reg.exe из закодированного RegistryKeyPath («HKLM|32|SOFTWARE\…\X» → «HKLM\SOFTWARE\WOW6432Node\…\X»).</summary>
    public static string? ToRegExePath(string registryKeyPath)
    {
        var parts = registryKeyPath.Split('|', 3);
        if (parts.Length != 3)
        {
            return null;
        }

        var (hive, view, sub) = (parts[0], parts[1], parts[2]);
        // Для 32-битной ветки реальный путь идёт через WOW6432Node.
        if (view == "32" && sub.StartsWith(UninstallRoot, StringComparison.OrdinalIgnoreCase))
        {
            sub = Wow6432Root + sub[UninstallRoot.Length..];
        }

        return $"{hive}\\{sub}";
    }

    private static bool TryRemoveEmptyInstallFolder(string? installLocation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            {
                return false;
            }

            // Убираем только ПУСТУЮ (без файлов) папку — чтобы не задеть возможные данные пользователя/сохранения.
            if (Directory.EnumerateFiles(installLocation, "*", SearchOption.AllDirectories).Any())
            {
                return false;
            }

            return RecycleBin.TrySendDirectory(installLocation);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Полное удаление по «следу установки»: всё, что установщик добавил (файлы/папки → Корзину, ветки реестра →
    /// с бэкапом перед удалением). Всё обратимо. После очистки след удаляется. Возвращает число вычищенных элементов.
    /// </summary>
    private int TryRemoveTracedLeftovers(string programName)
    {
        var trace = _traceStore.Find(programName);
        if (trace is null)
        {
            return 0;
        }

        var removed = 0;

        // Файлы/папки — в Корзину. Родительские пути идут раньше дочерних (короче → длиннее),
        // поэтому удаление родителя убирает и вложенное, а на исчезнувшие дочерние просто ничего не делаем.
        foreach (var path in trace.AddedPaths.OrderBy(static p => p.Length))
        {
            try
            {
                if (Directory.Exists(path))
                {
                    // Защита от удаления корней/системных/общих папок вендоров (след установки мог поймать чужое).
                    if (PathSafety.IsSafeToDeleteFolder(path) && RecycleBin.TrySendDirectory(path))
                    {
                        removed++;
                    }
                }
                else if (File.Exists(path) && RecycleBin.TrySend(path))
                {
                    removed++;
                }
            }
            catch (Exception)
            {
                // один остаток не поддался — не срываем очистку остальных
            }
        }

        // Ветки реестра — бэкап ветки ПЕРЕД удалением (обратимость). Пути в следе уже вида «HKLM\SOFTWARE\…».
        foreach (var regPath in trace.AddedRegistryKeys)
        {
            try
            {
                if (!ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"query \"{regPath}\""))
                {
                    continue; // ветки уже нет — чистить нечего
                }

                if (_regBackup.Backup(regPath, $"Полное удаление: {programName}") is null)
                {
                    continue; // без успешного бэкапа не удаляем
                }

                if (ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"delete \"{regPath}\" /f"))
                {
                    removed++;
                }
            }
            catch (Exception)
            {
                // одна ветка не поддалась — продолжаем
            }
        }

        _traceStore.Remove(programName); // след использован — больше не нужен
        return removed;
    }

    private bool TryRemoveOrphanRegistryKey(string registryKeyPath, string programName)
    {
        try
        {
            var regPath = ToRegExePath(registryKeyPath);
            if (regPath is null)
            {
                return false;
            }

            // Осиротевшая ли запись: деинсталлятор её НЕ удалил (программа всё ещё «висит» в списке).
            if (!ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"query \"{regPath}\""))
            {
                return false; // ключа уже нет — деинсталлятор сам убрал, чистить нечего
            }

            // Бэкап ветки ПЕРЕД удалением (обратимость, ADR 0002); без успешного бэкапа не удаляем.
            if (_regBackup.Backup(regPath, $"Остаток удаления: {programName}") is null)
            {
                return false;
            }

            return ProcessRunner.RunSync(ProcessRunner.System("reg.exe"), $"delete \"{regPath}\" /f");
        }
        catch (Exception)
        {
            return false;
        }
    }
}
