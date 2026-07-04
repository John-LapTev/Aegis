using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный каталог обновлений драйверов через агент Windows Update (WUA, COM <c>Microsoft.Update.Session</c>).
/// Спрашивает у Windows применимые, но НЕ установленные драйверы (Type='Driver' and IsInstalled=0) — это официальный
/// точный источник «есть более свежий драйвер» для ЛЮБОГО устройства, а не только NVIDIA. Позднее связывание (COM
/// доступен лишь в рантайме Windows): вне Windows / без сети / при ошибке — пустой список. Результат кешируется на
/// сессию: онлайн-поиск WUA дорогой, повторный скан берёт готовое.
/// </summary>
public sealed class WindowsUpdateDriverCatalog : IDriverUpdateCatalog
{
    // ServerSelection.ssWindowsUpdate — брать публичный каталог Windows Update (даже если ПК управляется WSUS).
    private const int ServerWindowsUpdate = 2;

    private readonly object _gate = new();
    private IReadOnlyList<DriverUpdateOffer>? _cached;

    public Task<IReadOnlyList<DriverUpdateOffer>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_cached is not null)
            {
                return Task.FromResult(_cached);
            }
        }

        return Task.Run(() =>
        {
            var offers = OperatingSystem.IsWindows() ? QueryWindows(cancellationToken) : [];
            lock (_gate)
            {
                _cached ??= offers;
                return _cached;
            }
        }, cancellationToken);
    }

    public Task<DriverInstallResult> InstallAsync(string updateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateId))
        {
            return Task.FromResult(DriverInstallResult.Failed("Неизвестно, какой драйвер ставить."));
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(DriverInstallResult.Failed("Установка драйверов доступна только в Windows."));
        }

        return Task.Run(() => InstallWindows(updateId, cancellationToken), cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    private static DriverInstallResult InstallWindows(string updateId, CancellationToken cancellationToken)
    {
        try
        {
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            var collectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
            if (sessionType is null || collectionType is null)
            {
                return DriverInstallResult.Failed("Служба Windows Update недоступна.");
            }

            dynamic session = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            TrySet(() => searcher.ServerSelection = ServerWindowsUpdate);

            cancellationToken.ThrowIfCancellationRequested();
            dynamic result = searcher.Search("Type='Driver' and IsInstalled=0");
            dynamic updates = result.Updates;

            // Находим ровно то обновление, которое выбрал пользователь (по Identity.UpdateID).
            dynamic? target = null;
            int count = updates.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic candidate = updates.Item[i];
                var id = TryGetString(() => (string)candidate.Identity.UpdateID);
                if (string.Equals(id, updateId, StringComparison.OrdinalIgnoreCase))
                {
                    target = candidate;
                    break;
                }
            }

            if (target is null)
            {
                return DriverInstallResult.Failed("Это обновление драйвера больше не предлагается Windows (возможно, уже установлено).");
            }

            TrySet(() =>
            {
                if (!(bool)target!.EulaAccepted)
                {
                    target!.AcceptEula();
                }
            });

            dynamic toProcess = Activator.CreateInstance(collectionType)!;
            toProcess.Add(target);

            // Скачиваем выбранный драйвер…
            cancellationToken.ThrowIfCancellationRequested();
            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = toProcess;
            downloader.Download();

            // …и ставим его. Windows сохраняет предыдущий драйвер для отката (Диспетчер устройств → «Откатить»).
            cancellationToken.ThrowIfCancellationRequested();
            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = toProcess;
            dynamic installResult = installer.Install();

            // ResultCode: 2 — успешно, 3 — успешно с предупреждениями, остальное — неуспех.
            int code = TryGetInt(() => (int)installResult.ResultCode) ?? 0;
            var reboot = TryGetBool(() => (bool)installResult.RebootRequired) ?? false;
            if (code is 2 or 3)
            {
                var message = "Драйвер установлен." + (reboot ? " Нужна перезагрузка, чтобы он применился." : string.Empty);
                return DriverInstallResult.Ok(reboot, message);
            }

            return DriverInstallResult.Failed("Windows не смогла установить этот драйвер. Можно попробовать через «Приложения» Windows.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DriverInstallResult.Failed("Не удалось установить драйвер: " + ex.Message);
        }
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<DriverUpdateOffer> QueryWindows(CancellationToken cancellationToken)
    {
        var offers = new List<DriverUpdateOffer>();
        try
        {
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (sessionType is null)
            {
                return offers; // служба Windows Update недоступна
            }

            dynamic session = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            TrySet(() => searcher.ServerSelection = ServerWindowsUpdate);

            cancellationToken.ThrowIfCancellationRequested();
            // Онлайн-поиск: применимые, но не установленные драйверы. Может занять несколько секунд (сеть) — терпимо,
            // выполняется в фоновом потоке скана; отмена проверяется до запроса (сам COM-вызов прервать нельзя).
            dynamic result = searcher.Search("Type='Driver' and IsInstalled=0");
            dynamic updates = result.Updates;

            int count = updates.Count;
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic update = updates.Item[i];
                var title = TryGetString(() => (string)update.Title);
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                offers.Add(new DriverUpdateOffer
                {
                    Title = title!,
                    DeviceName = TryGetString(() => (string)update.DriverModel),
                    HardwareId = TryGetString(() => (string)update.DriverHardwareID),
                    Provider = TryGetString(() => (string)update.DriverProvider),
                    Date = TryGetDate(() => (DateTime)update.DriverVerDate),
                    UpdateId = TryGetString(() => (string)update.Identity.UpdateID),
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // WUA недоступен / нет сети / изменился API — возвращаем то, что успели (обычно пусто). Best-effort.
        }

        return offers;
    }

    private static void TrySet(Action set)
    {
        try
        {
            set();
        }
        catch (Exception)
        {
            // Свойство недоступно в этой конфигурации WUA — не критично, идём с настройками по умолчанию.
        }
    }

    private static string? TryGetString(Func<string> get)
    {
        try
        {
            var value = get();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryGetDate(Func<DateTime> get)
    {
        try
        {
            return get().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int? TryGetInt(Func<int> get)
    {
        try
        {
            return get();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool? TryGetBool(Func<bool> get)
    {
        try
        {
            return get();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
