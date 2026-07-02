using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Удаление ВЫБРАННЫХ элементов внутри большой папки (файлов и подпапок) — по умолчанию в Корзину Windows
/// (обратимо, ADR 0002), либо навсегда, если пользователь явно выбрал это (с предупреждением в UI). В отличие
/// от <see cref="JunkCleanupFix"/> удаляет и сами подпапки целиком, а не только файлы внутри них.
/// </summary>
public sealed class FolderItemsDeleteFix : IFix
{
    private readonly IReadOnlyList<string> _paths;
    private readonly bool _permanent;

    public FolderItemsDeleteFix(string findingId, ScanGroup group, IReadOnlyList<string> paths, bool permanent = false)
    {
        FindingId = findingId;
        Group = group;
        _paths = paths;
        _permanent = permanent;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    // Удаление в Корзину обратимо — медленная точка восстановления не нужна.
    public bool RequiresSystemRestorePoint => false;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var files = new List<string>();
            var directories = new List<string>();
            foreach (var path in _paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    files.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    directories.Add(path);
                }
            }

            return Task.FromResult(_permanent
                ? DeletePermanently(files, directories, cancellationToken)
                : DeleteToRecycleBin(files, directories, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось удалить выбранное: " + ex.Message));
        }
    }

    private static FixOutcome DeletePermanently(
        IReadOnlyList<string> files, IReadOnlyList<string> directories, CancellationToken cancellationToken)
    {
        // Пользователь явно выбрал «навсегда» (с предупреждением) — освобождаем место сразу.
        // Считаем реально удалённое: нельзя рапортовать «удалено навсегда», если всё осталось на диске.
        var deleted = 0;
        var failed = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Delete(file); deleted++; } catch (Exception) { failed++; /* занят/нет прав */ }
        }

        foreach (var dir in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { Directory.Delete(dir, recursive: true); deleted++; } catch (Exception) { failed++; /* занят/нет прав */ }
        }

        if (deleted == 0 && failed > 0)
        {
            return FixOutcome.Failed("Ничего не удалось удалить — файлы заняты другими программами или нет прав. " +
                                     "Закрой программы, которые могут их использовать, и попробуй ещё раз.");
        }

        return FixOutcome.OkWithoutBackup();
    }

    private static FixOutcome DeleteToRecycleBin(
        IReadOnlyList<string> files, IReadOnlyList<string> directories, CancellationToken cancellationToken)
    {
        // Файлы — одним пакетом (быстро); каждую папку — целиком в Корзину. Если что-то не удалось/отменено —
        // честно сообщаем, а не рапортуем «успех».
        var ok = RecycleBin.TrySendMany(files);
        foreach (var dir in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ok &= RecycleBin.TrySendDirectory(dir);
        }

        return ok
            ? FixOutcome.OkWithoutBackup()
            : FixOutcome.Failed("Часть элементов не удалось переместить в Корзину (заняты, нет прав или отменено).");
    }
}
