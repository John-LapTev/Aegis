using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Сжимает (уплотняет) базы браузера: файл переупаковывается, пустоты внутри исчезают. Данные не меняются —
/// история, закладки и пароли остаются прежними, поэтому «Вернуть» здесь не нужен и не обещается.
///
/// Безопасность: перед каждой базой проверяется, что браузер закрыт и файл цел; перед изменением делается
/// копия, и при любой неудаче она возвращается на место.
/// </summary>
public sealed class SqliteVacuumFix : IFix
{
    private readonly IReadOnlyList<string> _paths;

    public SqliteVacuumFix(string findingId, ScanGroup group, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        FindingId = findingId;
        Group = group;
        _paths = paths;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (_paths.Count == 0)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось определить, какие базы сжимать."));
        }

        // Пока шёл выбор, браузер могли открыть заново — тогда сжимать нельзя ни в коем случае.
        if (BrowserOf(_paths[0]) is { } browser && SqliteMaintenance.IsAnyProcessRunning(browser.Processes))
        {
            return Task.FromResult(FixOutcome.Failed(
                $"{browser.Name} сейчас запущен. Закрой его полностью и нажми ещё раз — сжимать базы открытого " +
                "браузера нельзя, это может испортить историю и закладки."));
        }

        long freed = 0;
        var compacted = 0;

        foreach (var path in _paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var gain = SqliteMaintenance.Compact(path);
            if (gain > 0)
            {
                freed += gain;
                compacted++;
            }
        }

        if (compacted == 0)
        {
            return Task.FromResult(FixOutcome.Failed(
                "Сжать базы не получилось — они либо уже уплотнены, либо заняты другой программой. " +
                "Данные не пострадали."));
        }

        return Task.FromResult(new FixOutcome
        {
            Success = true,
            BackupId = null, // данные не менялись — откатывать нечего
            Message = $"Сжато баз: {compacted}. Освободилось {HumanSize.Format(freed)}.",
        });
    }

    /// <summary>Какому браузеру принадлежит база (по расположению файла).</summary>
    private static BrowserDatabases? BrowserOf(string path)
    {
        foreach (var browser in BrowserDatabaseCatalog.Browsers)
        {
            var root = Environment.ExpandEnvironmentVariables(browser.ProfilesRoot);
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return browser;
            }
        }

        return null;
    }
}
