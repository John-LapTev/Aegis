using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Удаление ПАПКИ-остатка (от удалённой программы/игры) целиком в Корзину Windows — обратимо (ADR 0002):
/// папку можно вернуть из Корзины, место освобождается после её очистки. Безвозвратного удаления нет.
/// </summary>
public sealed class FolderRecycleFix : IFix
{
    private readonly string _path;
    private readonly bool _permanent;

    public FolderRecycleFix(string findingId, ScanGroup group, string path, bool permanent = false)
    {
        FindingId = findingId;
        Group = group;
        _path = path;
        _permanent = permanent;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    // Удаление папки обратимо через Корзину — медленная точка восстановления не нужна.
    public bool RequiresSystemRestorePoint => false;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (_permanent)
        {
            // Пользователь явно выбрал «удалить навсегда» (с предупреждением).
            try
            {
                if (Directory.Exists(_path))
                {
                    Directory.Delete(_path, recursive: true);
                }

                return Task.FromResult(FixOutcome.OkWithoutBackup());
            }
            catch (Exception ex)
            {
                return Task.FromResult(FixOutcome.Failed("Не удалось удалить папку: " + ex.Message));
            }
        }

        var sent = RecycleBin.TrySendDirectory(_path);
        return Task.FromResult(sent
            ? FixOutcome.OkWithoutBackup()
            : FixOutcome.Failed("Папку не убрали: она занята/нет прав, либо не помещается в Корзину (удалять " +
                                "безвозвратно не стали)." + FileLockInspector.DescribeLockers(_path)));
    }
}
