using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// «Удалить полностью» программу из автозапуска. Порядок (по договорённости с Иваном): если у программы есть штатный
/// деинсталлятор (она в списке установленных) — запускаем ЕГО и дочищаем остатки (переиспользуем
/// <see cref="IProgramUninstaller"/>, у которого уже есть чистка пустых папок, осиротевшего реестра и «следа установки»).
/// Если деинсталлятора нет — значит программа уже удалена, остались только следы (папки/AppData/реестр), из-за которых
/// она и всплыла в автозапуске/загрузке: чистим ИМЕННО эти остатки по имени (<see cref="ILeftoverService"/>) — запрос
/// Ивана 1298. Всё удаление обратимо (Корзина + бэкап реестра), с защитой от системных путей.
/// </summary>
public sealed class StartupProgramRemover : IStartupProgramRemover
{
    private readonly IInstalledProgramsProbe _programs;
    private readonly IProgramUninstaller _uninstaller;
    private readonly ILeftoverService _leftovers;

    public StartupProgramRemover(
        IInstalledProgramsProbe programs, IProgramUninstaller uninstaller, ILeftoverService leftovers)
    {
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(uninstaller);
        ArgumentNullException.ThrowIfNull(leftovers);
        _programs = programs;
        _uninstaller = uninstaller;
        _leftovers = leftovers;
    }

    public async Task<UninstallResult> RemoveAsync(
        string executablePath, string displayName, CancellationToken cancellationToken = default)
    {
        var installed = await _programs.FindAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var match = MatchProgram(installed, executablePath, displayName);

        // Вариант 1: штатный деинсталлятор + полная чистка остатков (самый безопасный «полный снос»).
        if (match is not null
            && (!string.IsNullOrWhiteSpace(match.UninstallCommand) || !string.IsNullOrWhiteSpace(match.QuietUninstallCommand)))
        {
            return await _uninstaller.UninstallAsync(match, cleanLeftovers: true, cancellationToken).ConfigureAwait(false);
        }

        // Вариант 2: деинсталлятора нет (программа уже удалена) — удаляем найденные ОСТАТКИ по имени: папку установки,
        // папки в профиле (Local/Roaming/LocalLow/ProgramData), осиротевшие ветки реестра.
        return await RemoveLeftoversAsync(executablePath, displayName, match, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Чистит остатки удалённой программы по имени (папки/файлы — в Корзину, реестр — с бэкапом). Честный итог.</summary>
    private async Task<UninstallResult> RemoveLeftoversAsync(
        string executablePath, string displayName, InstalledProgram? match, CancellationToken cancellationToken)
    {
        var name = CleanName(displayName);
        // Синтетическая «программа» для сканера остатков: имя + (если знаем) место установки/папка exe + данные найденной.
        var program = new InstalledProgram
        {
            Name = name,
            InstallLocation = match?.InstallLocation ?? FolderFromExe(executablePath),
            Publisher = match?.Publisher,
            RegistryKeyPath = match?.RegistryKeyPath ?? string.Empty,
        };

        var leftovers = await _leftovers.ScanAsync(program, cancellationToken).ConfigureAwait(false);
        if (leftovers.Count == 0)
        {
            return UninstallResult.Failed(
                $"У «{name}» не нашлось ни установщика, ни файлов-остатков — похоже, программа уже полностью удалена. " +
                "Осталась только запись в автозапуске, её убираем.");
        }

        var removed = await _leftovers.RemoveAsync(leftovers, cancellationToken).ConfigureAwait(false);
        return new UninstallResult
        {
            Success = true,
            Message = $"Установщика нет — похоже, программа уже была удалена. Убрал найденные остатки ({removed}): " +
                      "папки и записи в реестре (папки — в Корзину, реестр — с резервной копией, всё обратимо).",
        };
    }

    /// <summary>Имя программы без расширения .exe (из журнала загрузки приходит «Rave.exe» — для поиска остатков нужно «Rave»).</summary>
    private static string CleanName(string displayName)
    {
        var name = Path.GetFileNameWithoutExtension(displayName);
        return string.IsNullOrWhiteSpace(name) ? displayName : name;
    }

    /// <summary>Папка из пути к exe — только если путь содержит папку (а не голое имя файла из журнала загрузки).</summary>
    private static string? FolderFromExe(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var folder = Path.GetDirectoryName(executablePath);
        return string.IsNullOrWhiteSpace(folder) ? null : folder;
    }

    /// <summary>Ищет установленную программу по папке (exe внутри неё) или по имени.</summary>
    private static InstalledProgram? MatchProgram(
        IReadOnlyList<InstalledProgram> installed, string executablePath, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            var byLocation = installed.FirstOrDefault(p => ExeIsInside(executablePath, p.InstallLocation));
            if (byLocation is not null)
            {
                return byLocation;
            }
        }

        var name = Path.GetFileNameWithoutExtension(displayName);
        return string.IsNullOrWhiteSpace(name)
            ? null
            : installed.FirstOrDefault(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Лежит ли exe ВНУТРИ папки установки (со строгой проверкой границы папки, а не по префиксу строки).</summary>
    private static bool ExeIsInside(string executablePath, string? installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return false;
        }

        var root = installLocation.Replace('/', '\\').TrimEnd('\\');
        var exe = executablePath.Replace('/', '\\');
        return root.Length > 3 // не «C:\» — иначе совпадёт со всем
               && exe.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
    }

}
