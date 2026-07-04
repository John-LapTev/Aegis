using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// «Удалить полностью» программу из автозапуска. Порядок (по договорённости с Иваном): если у программы есть штатный
/// деинсталлятор (она в списке установленных) — запускаем ЕГО и дочищаем остатки (переиспользуем
/// <see cref="IProgramUninstaller"/>, у которого уже есть чистка пустых папок, осиротевшего реестра и «следа установки»).
/// Если деинсталлятора нет — убираем папку программы в Корзину (обратимо), с защитой от системных путей.
/// </summary>
public sealed class StartupProgramRemover : IStartupProgramRemover
{
    private readonly IInstalledProgramsProbe _programs;
    private readonly IProgramUninstaller _uninstaller;
    private readonly IForceDeleteService _forceDelete;

    public StartupProgramRemover(
        IInstalledProgramsProbe programs, IProgramUninstaller uninstaller, IForceDeleteService forceDelete)
    {
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(uninstaller);
        ArgumentNullException.ThrowIfNull(forceDelete);
        _programs = programs;
        _uninstaller = uninstaller;
        _forceDelete = forceDelete;
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

        // Вариант 2: деинсталлятора нет — убираем папку программы в Корзину (обратимо).
        // Папку берём из пути exe; если он без папки (из журнала загрузки приходит только имя «Rave.exe») —
        // из места установки найденной программы. Иначе удалять нечего (честно скажем об этом).
        var folder = FolderFromExe(executablePath) ?? match?.InstallLocation;
        return await RemoveByFolderAsync(folder).ConfigureAwait(false);
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

    private async Task<UninstallResult> RemoveByFolderAsync(string? folder)
    {
        // Папку определить не удалось (нет ни пути к exe, ни места установки) — честно, без выдумки про «системную».
        if (string.IsNullOrWhiteSpace(folder))
        {
            return UninstallResult.Failed(
                "У этой программы не нашлось ни деинсталлятора, ни папки для удаления — Windows сообщает только имя " +
                "файла. Проще всего удалить её через «Приложения» Windows (кнопка ниже).");
        }

        // Папка реально общая/системная (диск, Windows, Program Files, папка вендора) — целиком не трогаем.
        if (!PathSafety.IsSafeToDeleteFolder(folder))
        {
            return UninstallResult.Failed(
                $"Папку «{folder}» удалять целиком небезопасно — это общая или системная папка. " +
                "Удали программу через «Приложения» Windows (кнопка ниже).");
        }

        var result = await _forceDelete.DeleteAsync(folder).ConfigureAwait(false);
        return result.Success
            ? new UninstallResult { Success = true, Message = "Папка программы убрана в Корзину (вернуть можно). " + result.Message }
            : UninstallResult.Failed("Не удалось удалить папку программы: " + result.Message);
    }

}
