using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Раздел «Игры»: включение и выключение игрового режима. Режим временно освобождает компьютер под игру —
/// приостанавливает фоновые службы и программы, переключает питание, отключает игровую панель и лишнюю
/// задержку в сети. Всё возвращается обратно кнопкой «Выключить» (и переживает перезапуск программы).
/// </summary>
public sealed partial class GameModeViewModel : ObservableObject, IDisposable
{
    /// <summary>Как часто проверяем, запущена ли игра (при включённом авто-режиме).</summary>
    private static readonly TimeSpan DetectInterval = TimeSpan.FromSeconds(15);

    private readonly IGameModeService _service;
    private readonly GameModeSettingsStore _settings;
    private readonly CancellationTokenSource _lifetime = new();
    private Timer? _detectTimer;
    private bool _autoActivated;
    private bool _disposed;

    public GameModeViewModel(IGameModeService service, GameModeSettingsStore? settings = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        _settings = settings ?? new GameModeSettingsStore();

        var saved = _settings.Load();
        _pauseServices = saved.PauseServices;
        _closeBackgroundApps = saved.CloseBackgroundApps;
        _highPerformancePower = saved.HighPerformancePower;
        _disableGameBar = saved.DisableGameBar;
        _keepAwake = saved.KeepAwake;
        _reduceNetworkLatency = saved.ReduceNetworkLatency;
        _disableTransparency = saved.DisableTransparency;
        _autoDetectGames = saved.AutoDetectGames;
    }

    /// <summary>Что сейчас применено — для показа списком.</summary>
    public ObservableCollection<string> AppliedActions { get; } = [];

    /// <summary>Что не удалось сделать (частичный успех) — показываем честно, не прячем.</summary>
    public ObservableCollection<string> FailedActions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(ActionButtonText), nameof(StateHint))]
    private bool _isActive;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateHint))]
    private string? _detectedGame;

    [ObservableProperty]
    private string _statusText = string.Empty;

    // ─── Настройки (галочки) ───────────────────────────────────────────────────

    [ObservableProperty]
    private bool _pauseServices;

    [ObservableProperty]
    private bool _closeBackgroundApps;

    [ObservableProperty]
    private bool _highPerformancePower;

    [ObservableProperty]
    private bool _disableGameBar;

    [ObservableProperty]
    private bool _keepAwake;

    [ObservableProperty]
    private bool _reduceNetworkLatency;

    [ObservableProperty]
    private bool _disableTransparency;

    [ObservableProperty]
    private bool _autoDetectGames;

    public bool HasApplied => AppliedActions.Count > 0;

    public bool HasFailed => FailedActions.Count > 0;

    public string StateText => IsActive ? "Игровой режим включён" : "Игровой режим выключен";

    public string ActionButtonText => IsActive ? "Выключить игровой режим" : "Включить игровой режим";

    public string StateHint => IsActive
        ? DetectedGame is { Length: > 0 } game
            ? $"Включён автоматически — запущена игра ({game}). Выключится сам, когда ты выйдешь из игры."
            : "Компьютер отдаёт максимум игре. Нажми «Выключить», когда закончишь — всё вернётся как было."
        : DetectedGame is { Length: > 0 } running
            ? $"Сейчас запущена игра ({running}) — можно включить режим."
            : "Пока обычный режим работы. Включи перед игрой — станет плавнее.";

    /// <summary>Подтянуть текущее состояние (в том числе после перезапуска программы).</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        var status = await _service.GetStatusAsync(_lifetime.Token).ConfigureAwait(true);
        ApplyStatus(status);

        DetectedGame = await _service
            .DetectRunningGameAsync(CustomProcesses(), _lifetime.Token)
            .ConfigureAwait(true);

        StartOrStopDetection();
    }

    /// <summary>Включить или выключить режим (одна кнопка — по текущему состоянию).</summary>
    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = IsActive ? "Возвращаю всё как было…" : "Готовлю компьютер к игре…";

        try
        {
            var result = IsActive
                ? await _service.DeactivateAsync(_lifetime.Token).ConfigureAwait(true)
                : await _service.ActivateAsync(BuildOptions(), _lifetime.Token).ConfigureAwait(true);

            if (!result.Success)
            {
                StatusText = result.Message ?? "Не получилось. Попробуй ещё раз.";
                return;
            }

            IsActive = !IsActive;
            ShowResult(result);
            _settings.Save(BuildOptions());
        }
        catch (Exception ex)
        {
            StatusText = "Не получилось: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Сохранить настройки при изменении любой галочки (чтобы не переспрашивать каждый раз).</summary>
    partial void OnPauseServicesChanged(bool value) => SaveSettings();

    partial void OnCloseBackgroundAppsChanged(bool value) => SaveSettings();

    partial void OnHighPerformancePowerChanged(bool value) => SaveSettings();

    partial void OnDisableGameBarChanged(bool value) => SaveSettings();

    partial void OnKeepAwakeChanged(bool value) => SaveSettings();

    partial void OnReduceNetworkLatencyChanged(bool value) => SaveSettings();

    partial void OnDisableTransparencyChanged(bool value) => SaveSettings();

    partial void OnAutoDetectGamesChanged(bool value)
    {
        SaveSettings();
        StartOrStopDetection();
    }

    private void SaveSettings() => _settings.Save(BuildOptions());

    private GameModeOptions BuildOptions() => new()
    {
        PauseServices = PauseServices,
        CloseBackgroundApps = CloseBackgroundApps,
        HighPerformancePower = HighPerformancePower,
        DisableGameBar = DisableGameBar,
        KeepAwake = KeepAwake,
        ReduceNetworkLatency = ReduceNetworkLatency,
        DisableTransparency = DisableTransparency,
        AutoDetectGames = AutoDetectGames,
        CustomGameProcesses = CustomProcesses(),
    };

    private IReadOnlyList<string> CustomProcesses() => _settings.Load().CustomGameProcesses;

    private void ApplyStatus(GameModeStatus status)
    {
        IsActive = status.IsActive;
        AppliedActions.Clear();
        foreach (var action in status.AppliedActions)
        {
            AppliedActions.Add(action);
        }

        OnPropertyChanged(nameof(HasApplied));

        if (status.TriggeredByGame is { Length: > 0 } game)
        {
            DetectedGame = game;
        }
    }

    private void ShowResult(GameModeResult result)
    {
        AppliedActions.Clear();
        foreach (var action in result.Applied)
        {
            AppliedActions.Add(action);
        }

        FailedActions.Clear();
        foreach (var failure in result.Failed)
        {
            FailedActions.Add(failure);
        }

        OnPropertyChanged(nameof(HasApplied));
        OnPropertyChanged(nameof(HasFailed));

        StatusText = IsActive
            ? "Готово — компьютер настроен на игру."
            : "Готово — всё вернулось как было.";
    }

    // ─── Авто-режим: включаем при запуске игры, выключаем при выходе ───────────

    private void StartOrStopDetection()
    {
        if (_disposed)
        {
            return;
        }

        if (!AutoDetectGames)
        {
            _detectTimer?.Dispose();
            _detectTimer = null;
            return;
        }

        _detectTimer ??= new Timer(_ => _ = DetectAsync(), null, DetectInterval, DetectInterval);
    }

    /// <summary>
    /// Периодическая проверка: запущена игра — включаем режим, вышли из игры — выключаем. Сами выключаем
    /// ТОЛЬКО то, что сами и включили: если человек включил режим руками, выход из игры его не отменит.
    /// </summary>
    private async Task DetectAsync()
    {
        if (IsBusy || _disposed)
        {
            return;
        }

        try
        {
            var game = await _service.DetectRunningGameAsync(CustomProcesses(), _lifetime.Token).ConfigureAwait(false);
            DetectedGame = game;

            if (game is not null && !IsActive)
            {
                var result = await _service.ActivateAsync(BuildOptions(), _lifetime.Token).ConfigureAwait(false);
                if (result.Success)
                {
                    _autoActivated = true;
                    IsActive = true;
                    ShowResult(result);
                }

                return;
            }

            if (game is null && IsActive && _autoActivated)
            {
                var result = await _service.DeactivateAsync(_lifetime.Token).ConfigureAwait(false);
                if (result.Success)
                {
                    _autoActivated = false;
                    IsActive = false;
                    ShowResult(result);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Программа закрывается — молча выходим.
        }
        catch (Exception)
        {
            // Ошибка авто-проверки не должна ничего ломать: человек всегда может нажать кнопку сам.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _detectTimer?.Dispose();
        _detectTimer = null;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
