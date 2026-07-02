using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Запускает официальный поиск драйверов средствами Windows (<c>pnputil /scan-devices</c>) — Windows
/// пересканирует оборудование и установит доступные драйверы из своего хранилища/Windows Update.
/// Безопасно и универсально (без сторонних сайтов); драйверами управляет сама Windows.
/// </summary>
public sealed class DriverSearchFix : IFix
{
    public DriverSearchFix(string findingId) => FindingId = findingId;

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Drivers;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        // Через ProcessRunner (он вычитывает stdout/stderr) — иначе подробный вывод pnputil переполнял бы
        // буфер и поиск драйвера завис бы навсегда.
        var code = await ProcessRunner.RunAsync(
            ProcessRunner.System("pnputil.exe"), "/scan-devices", cancellationToken).ConfigureAwait(false);

        // pnputil может вернуть ненулевой код (например, «нужна перезагрузка»), но скан при этом выполнен;
        // провал — только если процесс не запустился (-1).
        return code < 0
            ? FixOutcome.Failed("Не удалось запустить поиск драйвера.")
            : FixOutcome.OkWithoutBackup();
    }
}
