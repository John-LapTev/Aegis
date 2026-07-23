using System.Text.RegularExpressions;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Удаляет старые версии драйверов из хранилища Windows (<c>pnputil /delete-driver</c>). Действие
/// НЕОБРАТИМО средствами программы: вернуть удалённый пакет можно только заново скачав драйвер. Поэтому
/// результат честно помечается как «без бэкапа» — кнопки «Вернуть» у него не будет. Общая страховка —
/// точка восстановления Windows, которую движок правок делает перед пакетом изменений.
/// </summary>
public sealed partial class DriverPackageDeleteFix : IFix
{
    /// <summary>Допустимое имя пакета: только «oemNN.inf» — защита от произвольного аргумента команды.</summary>
    [GeneratedRegex(@"^oem\d{1,6}\.inf$", RegexOptions.IgnoreCase)]
    private static partial Regex PackageNamePattern();

    private readonly IReadOnlyList<string> _packages;

    public DriverPackageDeleteFix(string findingId, ScanGroup group, IReadOnlyList<string> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);
        FindingId = findingId;
        Group = group;
        _packages = packages.Where(name => PackageNamePattern().IsMatch(name)).ToList();
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (_packages.Count == 0)
        {
            return FixOutcome.Failed("Не удалось определить, какие пакеты драйверов удалять.");
        }

        var deleted = 0;
        var failed = new List<string>();

        foreach (var package in _packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var code = await ProcessRunner
                .RunAsync(ProcessRunner.System("pnputil.exe"), $"/delete-driver {package}", cancellationToken)
                .ConfigureAwait(false);

            if (code == 0)
            {
                deleted++;
            }
            else
            {
                failed.Add(package);
            }
        }

        if (deleted == 0)
        {
            return FixOutcome.Failed(
                "Не удалось удалить старые версии драйвера. Обычно это значит, что версия всё-таки используется " +
                "устройством — тогда удалять её и не нужно.");
        }

        // Часть удалилась, часть нет — сообщаем честно, а не «всё готово».
        return failed.Count == 0
            ? FixOutcome.OkWithoutBackup()
            : new FixOutcome
            {
                Success = true,
                BackupId = null,
                Message = $"Удалено версий: {deleted}. Остались занятые устройством: {string.Join(", ", failed)}.",
            };
    }
}
