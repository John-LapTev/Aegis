using System;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Core.Models;
using Aegis.Scanners.Stress;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Раздел «Тесты»: запуск безопасной/углублённой проверки под нагрузкой, живая шкала (время, температуры,
/// прогресс), кнопка «Стоп» и итоговый вердикт. Сам тест крутится на фоне (<see cref="Task.Run(Action)"/>),
/// а прогресс приходит через <see cref="IProgress{T}"/> уже в UI-поток — интерфейс не подвисает.
/// </summary>
public sealed partial class StressTestViewModel : ObservableObject
{
    private readonly IStressTestEngine _engine;
    private readonly Action<StressTestResult> _onCompleted;
    private CancellationTokenSource? _cts;

    /// <summary>Идёт ли проверка прямо сейчас.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _isRunning;

    /// <summary>Доля выполнения 0..1 — для шкалы прогресса.</summary>
    [ObservableProperty]
    private double _progressFraction;

    /// <summary>«12 / 60 сек» — сколько прошло из запланированного.</summary>
    [ObservableProperty]
    private string _elapsedText = string.Empty;

    /// <summary>Текущая температура процессора, например «72 °C».</summary>
    [ObservableProperty]
    private string _cpuTempText = "—";

    /// <summary>Текущая температура видеокарты.</summary>
    [ObservableProperty]
    private string _gpuTempText = "—";

    /// <summary>«макс 78 °C» — пиковая температура за тест.</summary>
    [ObservableProperty]
    private string _maxTempText = string.Empty;

    /// <summary>Пояснение текущего состояния («Идёт проверка под нагрузкой…»).</summary>
    [ObservableProperty]
    private string _phaseText = "Готов к проверке.";

    /// <summary>Есть ли готовый итог для показа карточкой.</summary>
    [ObservableProperty]
    private bool _hasResult;

    /// <summary>Итоговый вердикт простыми словами.</summary>
    [ObservableProperty]
    private string _resultVerdict = string.Empty;

    /// <summary>Цвет итога (🟢/🟡/🔴).</summary>
    [ObservableProperty]
    private Severity _resultSeverity;

    public StressTestViewModel(IStressTestEngine engine, Action<StressTestResult> onCompleted)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(onCompleted);
        _engine = engine;
        _onCompleted = onCompleted;
    }

    /// <summary>Можно ли запускать новый тест (не во время уже идущего).</summary>
    public bool CanStart => !IsRunning;

    [RelayCommand]
    private Task StartSafe() => RunAsync(StressTestKind.CpuSafe);

    [RelayCommand]
    private Task StartDeep() => RunAsync(StressTestKind.CpuDeep);

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    private async Task RunAsync(StressTestKind kind)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        HasResult = false;
        ProgressFraction = 0;
        MaxTempText = string.Empty;
        CpuTempText = "—";
        GpuTempText = "—";
        PhaseText = kind == StressTestKind.CpuDeep
            ? "Идёт углублённая проверка под нагрузкой… можно нажать «Стоп» в любой момент."
            : "Идёт проверка под нагрузкой… можно нажать «Стоп» в любой момент.";

        _cts = new CancellationTokenSource();
        var progress = new Progress<StressTestProgress>(OnProgress);
        try
        {
            // Тест (нагрузка + опрос датчиков) — на фоне; прогресс маршалится в UI-поток сам.
            var result = await Task.Run(() => _engine.RunAsync(kind, progress, _cts.Token)).ConfigureAwait(true);

            ResultSeverity = result.Severity;
            ResultVerdict = result.Verdict;
            HasResult = true;
            PhaseText = "Проверка завершена.";
            _onCompleted(result);
        }
        catch (OperationCanceledException)
        {
            PhaseText = "Проверка остановлена.";
        }
        catch (Exception ex)
        {
            // Неожиданный сбой движка (создание потоков нагрузки, датчики) не должен ронять приложение —
            // показываем причину в статусе теста, а не крашим процесс через AsyncRelayCommand.
            PhaseText = "Не удалось выполнить проверку: " + ex.Message;
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void OnProgress(StressTestProgress p)
    {
        ProgressFraction = p.Fraction;
        ElapsedText = $"{p.ElapsedSeconds} / {p.PlannedSeconds} сек";
        CpuTempText = p.CpuCelsius is int c ? $"{c} °C" : "—";
        GpuTempText = p.GpuCelsius is int g ? $"{g} °C" : "—";
        MaxTempText = p.MaxCpuCelsius is int mc ? $"макс {mc} °C" : string.Empty;
    }
}
