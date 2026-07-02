using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>Пункт левого меню-рельса (Сканы, Бэкапы, О программе).</summary>
public sealed partial class NavSectionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public NavSectionViewModel(string key, string title, string glyph, Action<NavSectionViewModel>? onSelect = null, bool isActive = false)
    {
        Key = key;
        Title = title;
        Glyph = glyph;
        _isActive = isActive;
        if (onSelect is not null)
        {
            SelectCommand = new RelayCommand(() => onSelect(this));
        }
    }

    /// <summary>Ключ раздела (scans/backups/about).</summary>
    public string Key { get; }

    /// <summary>Название раздела (русское).</summary>
    public string Title { get; }

    /// <summary>Ключ SVG-иконки раздела (scan/backup/about) — рисуется кодом, без эмодзи.</summary>
    public string Glyph { get; }

    /// <summary>Раздел «Нейросети» — вместо значка показываем подпись «AI».</summary>
    public bool IsAi => Key == "ai";

    /// <summary>Команда перехода в раздел.</summary>
    public IRelayCommand? SelectCommand { get; }
}
