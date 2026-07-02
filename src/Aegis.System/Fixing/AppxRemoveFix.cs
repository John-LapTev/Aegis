using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Обратимое удаление встроенного UWP-приложения: сначала сохраняем запись для возврата
/// (<see cref="AppxRemovalBackupStore"/>), затем <c>Remove-AppxPackage</c> (только для текущего пользователя).
/// Вернуть приложение можно в разделе «Бэкапы» или бесплатно из Microsoft Store (ADR 0002).
/// </summary>
public sealed class AppxRemoveFix : IFix
{
    private readonly AppxRemovalBackupStore _backup;
    private readonly string _packageFullName;
    private readonly string _appName;

    public AppxRemoveFix(string findingId, string packageFullName, string appName, AppxRemovalBackupStore backup)
    {
        FindingId = findingId;
        _packageFullName = packageFullName;
        _appName = appName;
        _backup = backup;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var backupId = _backup.Backup(_packageFullName, _appName, "Удаление приложения: " + _appName);
        if (backupId is null)
        {
            return FixOutcome.Failed("Не удалось подготовить возврат приложения — удаление отменено.");
        }

        // -ErrorAction Stop: ошибка Remove-AppxPackage (часто НЕ завершающая) станет завершающей → ненулевой код,
        // иначе powershell выходит с 0 и мы бы соврали «удалено», хотя приложение осталось.
        var code = await ProcessRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package '{_packageFullName}' -ErrorAction Stop\"",
            cancellationToken).ConfigureAwait(false);

        if (code != 0)
        {
            _backup.Discard(backupId);
            return FixOutcome.Failed("Не удалось удалить приложение (возможно, оно системное или нужны права администратора).");
        }

        return FixOutcome.Ok(backupId);
    }
}
