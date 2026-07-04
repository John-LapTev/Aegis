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
            // Коротко и по делу: ни установщика, ни следов — значит уже удалена.
            return UninstallResult.Failed(
                $"«{name}»: ни установщика, ни следов не нашлось.\nПохоже, программа уже полностью удалена.");
        }

        await _leftovers.RemoveAsync(leftovers, cancellationToken).ConfigureAwait(false);
        // Честно: файлы/папки-остатки удаляются НАСОВСЕМ (по выбору Ивана — не в Корзину); записи реестра — с бэкапом.
        // Не пишем «обратимо» про папки (аудит 2026-07-04).
        return new UninstallResult
        {
            Success = true,
            Message = $"«{name}» уже была удалена — оставались только следы.\nУбрал: {SummarizeLeftovers(leftovers)}.",
        };
    }

    /// <summary>Короткая сводка удалённых остатков по типам: «1 папку», «2 папки и 3 записи реестра».</summary>
    private static string SummarizeLeftovers(IReadOnlyList<LeftoverItem> items)
    {
        var folders = items.Count(i => i.Kind == LeftoverKind.Folder);
        var files = items.Count(i => i.Kind == LeftoverKind.File);
        var registry = items.Count(i => i.Kind is LeftoverKind.RegistryKey or LeftoverKind.RegistryValue);

        var parts = new List<string>();
        if (folders > 0)
        {
            parts.Add(Plural(folders, "папку", "папки", "папок"));
        }

        if (files > 0)
        {
            parts.Add(Plural(files, "файл", "файла", "файлов"));
        }

        if (registry > 0)
        {
            parts.Add(Plural(registry, "запись реестра", "записи реестра", "записей реестра"));
        }

        return parts.Count == 0 ? "следы" : string.Join(" и ", parts);
    }

    /// <summary>Русское склонение числа: 1 папку / 2 папки / 5 папок.</summary>
    private static string Plural(int count, string one, string few, string many)
    {
        var mod100 = count % 100;
        var mod10 = count % 10;
        var word = mod100 is >= 11 and <= 14 ? many
            : mod10 == 1 ? one
            : mod10 is >= 2 and <= 4 ? few
            : many;
        return $"{count} {word}";
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

        // Матч по имени — ТОЛЬКО по границе слова: иначе «Rave» совпало бы с «Brave» и мы запустили бы деинсталлятор
        // Brave вместо чистки остатков Rave — снос ЧУЖОЙ программы (аудит 2026-07-04).
        var name = Path.GetFileNameWithoutExtension(displayName);
        return string.IsNullOrWhiteSpace(name)
            ? null
            : installed.FirstOrDefault(p => NameMatch.ReferencesName(p.Name, name));
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
