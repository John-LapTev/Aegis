using System.Collections.ObjectModel;
using Aegis.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>Фаза «шкалы сканирования» блока-вкладки: пусто → синяя заливка с миганием → зелёная → снова пусто.</summary>
public enum TabScanPhase
{
    /// <summary>Нейтральный вид (не сканируется / итог сброшен).</summary>
    Idle,

    /// <summary>Идёт проверка: синяя заливка слева-направо + мигание.</summary>
    Scanning,

    /// <summary>Проверка этого блока завершена в текущем проходе: зелёная заливка.</summary>
    Done,
}

/// <summary>Группа-вкладка: её находки, статус проверки и цветные счётчики по важности.</summary>
public sealed partial class ScanGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isScanned;

    [ObservableProperty]
    private bool _isScanning;

    /// <summary>Открытая (активная) вкладка — для синей окантовки (выделение текущего раздела).</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Фаза шкалы сканирования (для заливки блока во время «Проверить всё»/раздела).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowScanFill), nameof(IsScanningNow), nameof(IsScanDone))]
    private TabScanPhase _scanPhase = TabScanPhase.Idle;

    /// <summary>Показывать ли заливку шкалы (идёт проверка или блок только что просканирован в проходе).</summary>
    public bool ShowScanFill => ScanPhase != TabScanPhase.Idle;

    /// <summary>Последний блок в ряду (у него нет связки-«палочки» справа).</summary>
    public bool IsLast { get; set; }

    /// <summary>Идёт проверка этого блока (синяя заливка с миганием).</summary>
    public bool IsScanningNow => ScanPhase == TabScanPhase.Scanning;

    /// <summary>Блок просканирован в текущем проходе (зелёная заливка).</summary>
    public bool IsScanDone => ScanPhase == TabScanPhase.Done;

    public ScanGroupViewModel(
        ScanGroup group,
        string title,
        Func<ScanGroupViewModel, Task>? onScan = null)
    {
        Group = group;
        Title = title;
        if (onScan is not null)
        {
            ScanCommand = new AsyncRelayCommand(() => onScan(this));
        }
    }

    public ScanGroup Group { get; }

    public string Title { get; }

    public ObservableCollection<FindingViewModel> Findings { get; } = [];

    /// <summary>Команда «Проверить этот раздел».</summary>
    public IAsyncRelayCommand? ScanCommand { get; }

    public int Count => Findings.Count;

    public int ProblemCount => Findings.Count(f => f.EffectiveSeverity == Severity.Danger);

    public int WarningCount => Findings.Count(f => f.EffectiveSeverity == Severity.Warning);

    // «Советы» — только Info. Зелёное (OK / «проверено-безопасно» / вручную «Безопасно» → EffectiveSeverity=Ok)
    // НЕ считаем советом: пользователь не должен видеть цифру там, где по факту всё в норме.
    public int AdviceCount => Findings.Count(f => f.EffectiveSeverity == Severity.Info);

    public bool HasProblems => ProblemCount > 0;

    public bool HasWarnings => WarningCount > 0;

    public bool HasAdvice => AdviceCount > 0;

    /// <summary>Заполнить находками по итогам проверки раздела.</summary>
    public void SetFindings(IEnumerable<FindingViewModel> findings)
    {
        Findings.Clear();
        // Авто-сортировка по важности (по умолчанию, во всех разделах): сперва проблемы, затем
        // «внимание», потом «советы», и в самом низу — то, что в норме (OK). OrderBy стабилен,
        // поэтому внутри одной важности исходный порядок сканера сохраняется.
        foreach (var finding in findings.OrderBy(f => SeverityRank(f.EffectiveSeverity)))
        {
            Findings.Add(finding);
        }

        IsScanned = true;
        NotifyCounts();
    }

    /// <summary>Ранг важности для сортировки: меньше — выше в списке (проблемы вверху, «в норме» внизу).</summary>
    private static int SeverityRank(Severity severity) => severity switch
    {
        Severity.Danger => 0,
        Severity.Warning => 1,
        Severity.Info => 2,
        Severity.Ok => 3,
        _ => 4,
    };

    /// <summary>Пересчитать счётчики (после изменения пометок «Безопасно»).</summary>
    public void NotifyCounts()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ProblemCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(AdviceCount));
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasAdvice));
    }
}
