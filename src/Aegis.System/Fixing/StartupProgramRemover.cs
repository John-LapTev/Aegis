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
        return await RemoveByFolderAsync(executablePath).ConfigureAwait(false);
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

    private async Task<UninstallResult> RemoveByFolderAsync(string executablePath)
    {
        var folder = string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(folder) || !PathSafety.IsSafeToDeleteFolder(folder))
        {
            return UninstallResult.Failed(
                "У этой программы нет деинсталлятора, а её папку удалять небезопасно (похоже на системную). " +
                "Лучше удалить её вручную или через «Параметры» Windows.");
        }

        var result = await _forceDelete.DeleteAsync(folder).ConfigureAwait(false);
        return result.Success
            ? new UninstallResult { Success = true, Message = "Папка программы убрана в Корзину (вернуть можно). " + result.Message }
            : UninstallResult.Failed("Не удалось удалить папку программы: " + result.Message);
    }

}
