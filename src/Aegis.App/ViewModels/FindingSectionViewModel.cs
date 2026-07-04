using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Подсекция списка находок внутри вкладки (например, в «Мусоре»: диски, безопасная чистка, крупные
/// файлы, крупные папки, дубликаты — чтобы разные виды не смешивались). Пустой <see cref="Title"/> —
/// секция без заголовка (раздел выглядит как обычный плоский список). Заголовок можно свернуть/развернуть.
/// </summary>
public sealed partial class FindingSectionViewModel : ObservableObject, global::System.IDisposable
{
    public FindingSectionViewModel(string title, IEnumerable<FindingViewModel> findings)
    {
        Title = title;
        Findings = new ObservableCollection<FindingViewModel>(findings);
        // Следим за галочками внутри подсекции — чтобы подпись кнопки «Выделить»/«Снять» была актуальной,
        // даже если пользователь отмечает пункты вручную.
        foreach (var finding in Findings)
        {
            finding.PropertyChanged += OnFindingPropertyChanged;
        }
    }

    private void OnFindingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FindingViewModel.IsSelected) || e.PropertyName == nameof(FindingViewModel.IsFixed))
        {
            OnPropertyChanged(nameof(SelectButtonLabel));
            OnPropertyChanged(nameof(HasSelectable)); // видимость кнопки «Выделить/Снять» при смене IsFixed
        }
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

    /// <summary>Есть ли в этой подсекции что выделять галочкой — для кнопки «Выделить»/«Снять» на её заголовке.</summary>
    public bool HasSelectable => Findings.Any(f => f.CanBatchSelect && !f.IsFixed);

    /// <summary>Все выделяемые блоки подсекции уже отмечены — тогда кнопка предлагает «Снять».</summary>
    private bool AllSelected
    {
        get
        {
            var selectable = Findings.Where(f => f.CanBatchSelect && !f.IsFixed).ToList();
            return selectable.Count > 0 && selectable.All(f => f.IsSelected);
        }
    }

    /// <summary>Подпись кнопки-тумблера: «Снять», если весь раздел уже выделен, иначе «Выделить» (запрос Ивана 1323).</summary>
    public string SelectButtonLabel => AllSelected ? "Снять" : "Выделить";

    /// <summary>Тумблер выделения этой подсекции: не всё выделено → выделить всё; всё выделено → снять. Другие не трогает.</summary>
    [RelayCommand]
    private void SelectSection()
    {
        var select = !AllSelected;
        foreach (var finding in Findings.Where(f => f.CanBatchSelect && !f.IsFixed))
        {
            finding.IsSelected = select;
        }

        OnPropertyChanged(nameof(SelectButtonLabel));
    }

    /// <summary>Отписка от находок (секции пересоздаются при каждом обновлении списка — иначе подписки копятся).</summary>
    public void Dispose()
    {
        foreach (var finding in Findings)
        {
            finding.PropertyChanged -= OnFindingPropertyChanged;
        }
    }
}
