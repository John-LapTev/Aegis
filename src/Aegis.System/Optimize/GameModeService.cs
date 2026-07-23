using System.Diagnostics;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;

namespace Aegis.System.Optimize;

/// <summary>
/// Игровой режим: временно освобождает компьютер под игру и возвращает всё обратно при выключении.
/// Прежнее состояние сохраняется на диск ПЕРЕД первым изменением — поэтому «выключить» работает даже
/// после перезапуска программы, а не только в текущем сеансе.
/// </summary>
public sealed class GameModeService : IGameModeService
{
    private readonly GameModeSnapshotStore _snapshots;
    private readonly GameModeActions _actions = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GameModeService(GameModeSnapshotStore? snapshots = null) => _snapshots = snapshots ?? new GameModeSnapshotStore();

    public Task<GameModeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _snapshots.Load();
        if (snapshot is null)
        {
            return Task.FromResult(GameModeStatus.Inactive);
        }

        return Task.FromResult(new GameModeStatus
        {
            IsActive = true,
            ActivatedAt = snapshot.ActivatedAt,
            TriggeredByGame = snapshot.TriggeredByGame,
            AppliedActions = DescribeSnapshot(snapshot),
        });
    }

    public async Task<GameModeResult> ActivateAsync(GameModeOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!OperatingSystem.IsWindows())
        {
            return GameModeResult.Error("Игровой режим работает только в Windows.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_snapshots.Load() is not null)
            {
                return GameModeResult.Error("Игровой режим уже включён.");
            }

            var applied = new List<string>();
            var failed = new List<string>();
            var services = new List<GameModeServiceState>();
            var registryValues = new List<GameModeRegistryState>();
            var closedApps = new List<string>();
            string? powerScheme = null;

            var game = await DetectRunningGameAsync(options.CustomGameProcesses, cancellationToken).ConfigureAwait(false);

            // Реестр трогаем первым: это самое быстрое и точно обратимое действие.
            if (options.DisableGameBar)
            {
                registryValues.AddRange(_actions.DisableGameBar());
                applied.Add("Отключены игровая панель и запись видео Xbox");
            }

            if (options.DisableTransparency)
            {
                registryValues.Add(_actions.DisableTransparency());
                applied.Add("Отключена прозрачность окон");
            }

            if (options.ReduceNetworkLatency)
            {
                var network = _actions.ReduceNetworkLatency();
                registryValues.AddRange(network);
                if (network.Count > 0)
                {
                    applied.Add("Убрана лишняя задержка в сети");
                }
                else
                {
                    failed.Add("Не удалось настроить сеть — нужны права администратора");
                }
            }

            if (options.HighPerformancePower)
            {
                powerScheme = await _actions.SetHighPerformanceAsync(cancellationToken).ConfigureAwait(false);
                if (powerScheme is not null)
                {
                    applied.Add("Питание переключено на «Высокую производительность»");
                }
                else
                {
                    failed.Add("Не удалось переключить схему питания");
                }
            }

            if (options.PauseServices)
            {
                services.AddRange(await _actions.PauseServicesAsync(cancellationToken).ConfigureAwait(false));
                if (services.Count > 0)
                {
                    applied.Add($"Приостановлены фоновые службы: {services.Count}");
                }
            }

            if (options.CloseBackgroundApps)
            {
                closedApps.AddRange(_actions.CloseBackgroundApps(cancellationToken));
                if (closedApps.Count > 0)
                {
                    applied.Add("Закрыты фоновые программы: " + string.Join(", ", closedApps.Distinct()));
                }
            }

            if (options.KeepAwake)
            {
                GameModeActions.KeepAwake(true);
                applied.Add("Компьютер не будет засыпать во время игры");
            }

            var snapshot = new GameModeSnapshot
            {
                ActivatedAt = DateTimeOffset.Now,
                Services = services,
                RegistryValues = registryValues,
                PowerSchemeGuid = powerScheme,
                ClosedApps = closedApps.Distinct().ToList(),
                TriggeredByGame = game,
            };

            _snapshots.Save(snapshot);
            return GameModeResult.Ok(applied, failed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return GameModeResult.Error("Не удалось включить игровой режим: " + ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GameModeResult> DeactivateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = _snapshots.Load();
            if (snapshot is null)
            {
                return GameModeResult.Error("Игровой режим не включён.");
            }

            var applied = new List<string>();
            var failed = new List<string>();

            var restoredValues = _actions.RestoreRegistryValues(snapshot.RegistryValues);
            if (restoredValues > 0)
            {
                applied.Add("Возвращены настройки игровой панели и сети");
            }

            if (snapshot.PowerSchemeGuid is { Length: > 0 } guid)
            {
                if (await _actions.RestorePowerSchemeAsync(guid, cancellationToken).ConfigureAwait(false))
                {
                    applied.Add("Возвращена прежняя схема питания");
                }
                else
                {
                    failed.Add("Не удалось вернуть прежнюю схему питания — проверь настройки электропитания");
                }
            }

            var restartedServices = await _actions.RestoreServicesAsync(snapshot.Services, cancellationToken).ConfigureAwait(false);
            var expected = snapshot.Services.Count(s => s.WasRunning);
            if (restartedServices > 0)
            {
                applied.Add($"Снова запущены службы: {restartedServices}");
            }

            if (restartedServices < expected)
            {
                failed.Add($"Не удалось запустить обратно служб: {expected - restartedServices}. " +
                           "Они запустятся сами после перезагрузки компьютера.");
            }

            GameModeActions.KeepAwake(false);

            // Снимок удаляем в самом конце: если что-то упало раньше, режим останется «включённым»
            // и его можно будет выключить повторно, а не потерять состояние навсегда.
            _snapshots.Clear();

            if (snapshot.ClosedApps.Count > 0)
            {
                applied.Add("Закрытые программы запусти заново сам: " + string.Join(", ", snapshot.ClosedApps));
            }

            return GameModeResult.Ok(applied, failed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return GameModeResult.Error("Не удалось выключить игровой режим: " + ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<string?> DetectRunningGameAsync(
        IReadOnlyList<string> customProcesses,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var running = Process.GetProcesses();
            try
            {
                var names = running.Select(p => p.ProcessName).ToList();
                return Task.FromResult(GameProcessCatalog.FindRunningGame(names, customProcesses));
            }
            finally
            {
                foreach (var process in running)
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Не удалось перечислить процессы — просто считаем, что игра не найдена.
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>Человекочитаемый список того, что сейчас применено (по снимку).</summary>
    private static IReadOnlyList<string> DescribeSnapshot(GameModeSnapshot snapshot)
    {
        var actions = new List<string>();

        if (snapshot.Services.Count > 0)
        {
            actions.Add($"Приостановлены фоновые службы: {snapshot.Services.Count}");
        }

        if (snapshot.ClosedApps.Count > 0)
        {
            actions.Add("Закрыты фоновые программы: " + string.Join(", ", snapshot.ClosedApps));
        }

        if (snapshot.PowerSchemeGuid is { Length: > 0 })
        {
            actions.Add("Питание переключено на «Высокую производительность»");
        }

        if (snapshot.RegistryValues.Any(v => v.ValueName.StartsWith("GameDVR", StringComparison.OrdinalIgnoreCase)
                                             || v.ValueName == "AppCaptureEnabled"))
        {
            actions.Add("Отключены игровая панель и запись видео Xbox");
        }

        if (snapshot.RegistryValues.Any(v => v.ValueName is "TcpAckFrequency" or "TCPNoDelay"))
        {
            actions.Add("Убрана лишняя задержка в сети");
        }

        if (snapshot.RegistryValues.Any(v => v.ValueName == "EnableTransparency"))
        {
            actions.Add("Отключена прозрачность окон");
        }

        return actions;
    }
}
