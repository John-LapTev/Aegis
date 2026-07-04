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
}
