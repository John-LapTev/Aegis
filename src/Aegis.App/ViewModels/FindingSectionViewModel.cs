using System.Collections.ObjectModel;
using System.Linq;
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

    /// <summary>Суммарный размер находок секции (для чипа-навигации в «Мусоре»).</summary>
    public long SizeBytes
    {
        get
        {
            long total = 0;
            foreach (var finding in Findings)
            {
                total += finding.SizeBytes;
            }

            return total;
        }
    }

    public bool HasSize => SizeBytes > 0;

    /// <summary>Размер секции строкой («3.6 ГБ») — для чипа.</summary>
    public string SizeText => Aegis.Core.HumanSize.Format(SizeBytes);

    /// <summary>Подпись чипа: «Кэш приложений · 2.9 МБ» (или без размера, если его нет).</summary>
    public string ChipLabel => HasSize ? $"{Title} · {SizeText}" : Title;

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

    /// <summary>Есть ли в этой подсекции что выделять галочкой — для кнопки «Выделить» на её заголовке.</summary>
    public bool HasSelectable => Findings.Any(f => f.CanBatchSelect && !f.IsFixed);

    /// <summary>Выделить галочками ВСЕ блоки только этой подсекции (кнопка на заголовке, запрос Ивана 1314). Не трогает другие.</summary>
    [RelayCommand]
    private void SelectSection()
    {
        foreach (var finding in Findings.Where(f => f.CanBatchSelect && !f.IsFixed))
        {
            finding.IsSelected = true;
        }
    }
}
