using System.Security.Principal;

namespace Aegis.App.Services;

/// <inheritdoc />
public sealed class ElevationService : IElevationService
{
    /// <inheritdoc />
    public bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
