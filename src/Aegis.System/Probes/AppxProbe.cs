using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник встроенного UWP-хлама: берёт список установленных пакетов через PowerShell
/// (<c>Get-AppxPackage</c>) и оставляет только заведомо лишние по <see cref="AppxBloatCatalog"/>. Только читает.
/// </summary>
public sealed class AppxProbe : IAppxProbe
{
    public async Task<IReadOnlyList<AppxApp>> FindBloatAsync(CancellationToken cancellationToken = default)
    {
        var output = await ProcessRunner.RunForOutputAsync(
            "powershell.exe",
            "-NoProfile -NonInteractive -Command \"Get-AppxPackage | Select-Object -ExpandProperty PackageFullName\"",
            cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(output)
            ? []
            : AppxBloatCatalog.Match(output.Split('\n'));
    }
}
