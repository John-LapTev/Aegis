using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Удаление файла в Корзину Windows (а не на тот же диск) — привычно, восстанавливается из Корзины,
/// и место реально освобождается после её очистки. Для мусора/больших/дублирующихся файлов.
/// </summary>
public sealed class RecycleBinFix : IFix
{
    private readonly string _path;
    private readonly bool _permanent;

    public RecycleBinFix(string findingId, ScanGroup group, string path, bool permanent = false)
    {
        FindingId = findingId;
        Group = group;
        _path = path;
        _permanent = permanent;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    // Удаление файла обратимо через Корзину — медленная точка восстановления не нужна.
    public bool RequiresSystemRestorePoint => false;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return Task.FromResult(FixOutcome.OkWithoutBackup());
        }

        if (_permanent)
        {
            // Пользователь явно выбрал «удалить навсегда» (с предупреждением).
            try
            {
                File.Delete(_path);
                return Task.FromResult(FixOutcome.OkWithoutBackup());
            }
            catch (Exception ex)
            {
                return Task.FromResult(FixOutcome.Failed("Не удалось удалить файл: " + ex.Message));
            }
        }

        return Task.FromResult(RecycleBin.TrySend(_path)
            ? FixOutcome.OkWithoutBackup()
            : FixOutcome.Failed("Не удалось удалить файл." + FileLockInspector.DescribeLockers(_path)));
    }
}
