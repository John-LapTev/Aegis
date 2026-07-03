using System.Linq;
using System.Threading.Tasks;
using Aegis.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Aegis.App.Views.Sections;

public partial class ForceDeleteView : UserControl
{
    public ForceDeleteView() => InitializeComponent();

    /// <summary>Выбрать файл и грубо удалить (закрыть мешающие процессы → в Корзину).</summary>
    private async void OnForceDeleteFileClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null)
            {
                return;
            }

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите файл, который не удаляется",
                AllowMultiple = false,
            }).ConfigureAwait(true);
            await ForceDeletePickedAsync(files.FirstOrDefault()?.TryGetLocalPath()).ConfigureAwait(true);
        }
        catch (global::System.Exception)
        {
            // Диалог отменён/недоступен — молча, чтобы не ронять UI.
        }
    }

    /// <summary>Выбрать папку и грубо удалить.</summary>
    private async void OnForceDeleteFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null)
            {
                return;
            }

            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Выберите папку, которая не удаляется",
                AllowMultiple = false,
            }).ConfigureAwait(true);
            await ForceDeletePickedAsync(folders.FirstOrDefault()?.TryGetLocalPath()).ConfigureAwait(true);
        }
        catch (global::System.Exception)
        {
            // Диалог отменён/недоступен — молча.
        }
    }

    private async Task ForceDeletePickedAsync(string? path)
    {
        if (!string.IsNullOrEmpty(path) && DataContext is MainWindowViewModel vm)
        {
            await vm.Dashboard.ForceDeleteAsync(path).ConfigureAwait(true);
        }
    }
}
