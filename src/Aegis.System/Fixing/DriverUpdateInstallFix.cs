using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Fixing;

/// <summary>
/// Установка драйвера прямо из программы через Windows Update (по <c>updateId</c> из находки) — скачивает и ставит
/// именно выбранный драйвер, без переходов на сайт (запрос Ивана). Откат — штатный: Windows хранит предыдущий драйвер
/// (Диспетчер устройств → «Откатить»), плюс точки восстановления. Кнопки «Вернуть» нет (как у поиска драйвера/winget).
/// </summary>
public sealed class DriverUpdateInstallFix : IFix
{
    private readonly IDriverUpdateCatalog _catalog;
    private readonly string _updateId;

    public DriverUpdateInstallFix(string findingId, string updateId, IDriverUpdateCatalog catalog)
    {
        FindingId = findingId;
        _updateId = updateId;
        _catalog = catalog;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Drivers;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _catalog.InstallAsync(_updateId, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? FixOutcome.OkWithoutBackup(result.RequiresReboot) with { Message = result.Message ?? "Драйвер установлен." }
            : FixOutcome.Failed(result.Message ?? "Не удалось установить драйвер.");
    }
}
