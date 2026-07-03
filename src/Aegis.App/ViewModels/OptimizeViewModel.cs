using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>Один фоновый процесс в списке оптимизации: понятное имя, память, галочка «закрыть».</summary>
public sealed partial class OptimizableProcessViewModel : ObservableObject
{
    public OptimizableProcessViewModel(OptimizableProcess process) => Process = process;

    public OptimizableProcess Process { get; }

    public string DisplayName => Process.DisplayName;
    public string RawName => Process.Name;
    public string Description => Process.Description;
    public string MemoryText => HumanSize.Format(Process.MemoryBytes);

    [ObservableProperty]
    private bool _isSelected = true;
}

/// <summary>
/// Раздел «Оптимизация»: честно показывает занятость ОЗУ и фоновые процессы (обновляторы/помощники), которые
/// можно безопасно закрыть, чтобы освободить память. Без выдуманных цифр — только реальная память до/после.
/// </summary>
public sealed partial class OptimizeViewModel : ObservableObject
{
    private readonly IMemoryOptimizer _optimizer;

    public OptimizeViewModel(IMemoryOptimizer optimizer) => _optimizer = optimizer;

    public ObservableCollection<OptimizableProcessViewModel> Processes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProcesses))]
    private bool _loaded;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _memoryText = string.Empty;

    [ObservableProperty]
    private int _memoryPercent;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool HasProcesses => Processes.Count > 0;

    /// <summary>Обновить снимок памяти и список безопасно-закрываемых фоновых процессов.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var state = await _optimizer.ReadAsync().ConfigureAwait(true);
            ApplyMemory(state.Memory);

            Processes.Clear();
            foreach (var process in state.Closeable)
            {
                Processes.Add(new OptimizableProcessViewModel(process));
            }

            OnPropertyChanged(nameof(HasProcesses));
            Loaded = true;
            StatusText = Processes.Count > 0
                ? "Эти фоновые процессы (обновляторы и помощники) можно закрыть — они сами запустятся снова, когда понадобятся."
                : "Лишних фоновых процессов не нашлось — память уже используется по делу.";
        }
        catch (global::System.Exception ex)
        {
            StatusText = "Не удалось прочитать процессы: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Закрыть отмеченные фоновые процессы и показать РЕАЛЬНУЮ память после (без выдуманных цифр).</summary>
    [RelayCommand]
    private async Task FreeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var pids = Processes.Where(p => p.IsSelected).SelectMany(p => p.Process.ProcessIds).ToList();
        if (pids.Count == 0)
        {
            StatusText = "Ничего не отмечено.";
            return;
        }

        IsBusy = true;
        var before = MemoryPercent;
        try
        {
            var stopped = await _optimizer.StopAsync(pids).ConfigureAwait(true);
            var state = await _optimizer.ReadAsync().ConfigureAwait(true);
            ApplyMemory(state.Memory);

            Processes.Clear();
            foreach (var process in state.Closeable)
            {
                Processes.Add(new OptimizableProcessViewModel(process));
            }

            OnPropertyChanged(nameof(HasProcesses));
            var delta = before - MemoryPercent;
            var freed = delta > 0 ? $" Память: было занято {before}%, стало {MemoryPercent}%." : string.Empty;
            StatusText = $"Закрыто процессов: {stopped}.{freed}";
        }
        catch (global::System.Exception ex)
        {
            StatusText = "Не удалось закрыть процессы: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyMemory(MemorySnapshot memory)
    {
        MemoryPercent = memory.UsedPercent;
        MemoryText = memory.TotalBytes > 0
            ? $"Занято {HumanSize.Format(memory.UsedBytes)} из {HumanSize.Format(memory.TotalBytes)} ({memory.UsedPercent}%)"
            : "Не удалось прочитать память";
    }
}
