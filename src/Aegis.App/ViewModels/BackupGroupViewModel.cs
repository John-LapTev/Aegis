using System.Collections.ObjectModel;

namespace Aegis.App.ViewModels;

/// <summary>Группа бэкапов одного вида (для раскрывающегося списка в разделе «Бэкапы»).</summary>
public sealed class BackupGroupViewModel
{
    public BackupGroupViewModel(string title, IEnumerable<BackupItemViewModel> items)
    {
        Title = title;
        Items = new ObservableCollection<BackupItemViewModel>(items);
    }

    public string Title { get; }

    public ObservableCollection<BackupItemViewModel> Items { get; }

    public int Count => Items.Count;
}
