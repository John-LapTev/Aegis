using System.Collections.Generic;
using System.Linq;
using Aegis.Core;
using Aegis.Core.Models;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aegis.App.Views;

/// <summary>
/// Окно «Остатки программы» (в духе Revo): после удаления показывает найденные хвосты (папки, файлы, ветки реестра)
/// с галочками. «Удалить выбранное» возвращает отмеченные остатки, «Оставить» — пустой список. Всё удаление обратимо.
/// </summary>
public partial class LeftoverConfirmWindow : Window
{
    private readonly List<CheckableLeftover> _items = [];

    public LeftoverConfirmWindow()
    {
        InitializeComponent();
    }

    public LeftoverConfirmWindow(string programName, IReadOnlyList<LeftoverItem> found, bool fullyRemoved = true) : this()
    {
        TitleText.Text = fullyRemoved
            ? $"Остатки после удаления «{programName}»"
            : $"«{programName}» удалилась НЕ до конца";

        IntroText.Text = fullyRemoved
            ? "Программа удалена. Aegis нашёл вот что от неё осталось. Отметь, что вычистить — файлы и папки удалятся " +
              "НАСОВСЕМ (это остатки уже удалённой программы), а ветки реестра — с бэкапом (можно вернуть из раздела «Бэкапы»)."
            : "Внимание: штатный деинсталлятор НЕ убрал программу до конца — возможно, нужна перезагрузка, или это " +
              "лаунчер/игра. Ниже — её файлы и записи. Удаляй ТОЛЬКО если уверен, что программа больше не нужна: файлы и " +
              "папки удалятся НАСОВСЕМ, ветки реестра — с бэкапом. Сомневаешься — нажми «Оставить» и удали программу " +
              "штатно или после перезагрузки.";

        // Группируем: сначала файлы/папки, затем всё реестровое (ветки и записи автозапуска).
        _items = found
            .OrderBy(i => IsRegistry(i.Kind) ? 1 : 0)
            .ThenBy(i => i.Display, global::System.StringComparer.OrdinalIgnoreCase)
            .Select(i => new CheckableLeftover(i))
            .ToList();
        ItemsList.ItemsSource = _items;

        var files = found.Count(i => !IsRegistry(i.Kind));
        var registry = found.Count(i => IsRegistry(i.Kind));
        SummaryText.Text = $"Найдено: файлов и папок — {files}, записей реестра — {registry}.";
    }

    private static bool IsRegistry(LeftoverKind kind) => kind is LeftoverKind.RegistryKey or LeftoverKind.RegistryValue;

    private void OnDeleteSelected(object? sender, RoutedEventArgs e) =>
        Close((IReadOnlyList<LeftoverItem>)_items.Where(i => i.IsSelected).Select(i => i.Item).ToList());

    private void OnKeep(object? sender, RoutedEventArgs e) =>
        Close((IReadOnlyList<LeftoverItem>)new List<LeftoverItem>());
}

/// <summary>Строка списка остатков с галочкой (по умолчанию выбрана).</summary>
public sealed partial class CheckableLeftover : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    public CheckableLeftover(LeftoverItem item) => Item = item;

    public LeftoverItem Item { get; }

    public string Display => Item.Display;

    public string SizeText => Item.Kind switch
    {
        LeftoverKind.RegistryKey => "ветка реестра",
        LeftoverKind.RegistryValue => "автозапуск (реестр)",
        _ => Item.SizeBytes > 0 ? HumanSize.Format(Item.SizeBytes) : string.Empty,
    };

    public string IconKey => Item.Kind switch
    {
        LeftoverKind.RegistryKey or LeftoverKind.RegistryValue => "code",
        LeftoverKind.File => "file",
        _ => "folder",
    };
}
