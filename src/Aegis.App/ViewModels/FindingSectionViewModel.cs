using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Подсекция списка находок внутри вкладки (например, в «Мусоре»: диски, безопасная чистка, крупные
/// файлы, крупные папки, дубликаты — чтобы разные виды не смешивались). Пустой <see cref="Title"/> —
/// секция без заголовка (раздел выглядит как обычный плоский список). Заголовок можно свернуть/развернуть.
/// </summary>
public sealed partial class FindingSectionViewModel : ObservableObject
{
    public FindingSectionViewModel(string title, IEnumerable<FindingViewModel> findings)
    {
        Title = title;
        Findings = new ObservableCollection<FindingViewModel>(findings);
    }

    public string Title { get; }

    /// <summary>Показывать ли заголовок секции (скрываем для безымянной единственной секции).</summary>
    public bool HasTitle => Title.Length > 0;

    public ObservableCollection<FindingViewModel> Findings { get; }

    public int Count => Findings.Count;

    /// <summary>Развёрнута ли секция (по клику на заголовок можно свернуть, чтобы не мешала).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    private bool _isExpanded = true;

    /// <summary>Ключ стрелки-индикатора: вниз — развёрнуто, вправо — свёрнуто.</summary>
    public string ChevronGlyph => IsExpanded ? "chevron-down" : "chevron-right";

    /// <summary>Заголовок со счётчиком, например «Крупные файлы (12)».</summary>
    public string TitleWithCount => $"{Title} ({Count})";

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}
