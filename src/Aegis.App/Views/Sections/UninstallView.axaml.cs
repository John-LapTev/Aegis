using System.Linq;
using Aegis.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Aegis.App.Views.Sections;

public partial class UninstallView : UserControl
{
    public UninstallView() => InitializeComponent();

    /// <summary>«Удаление программ»: выбрать установщик и поставить программу с наблюдением (запомнить её «след»).</summary>
    private async void OnWatchInstallClick(object? sender, RoutedEventArgs e)
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
                Title = "Выберите установщик программы (.exe или .msi)",
                AllowMultiple = false,
            }).ConfigureAwait(true);

            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path) && DataContext is MainWindowViewModel vm)
            {
                await vm.Dashboard.WatchInstallAsync(path).ConfigureAwait(true);
            }
        }
        catch (global::System.Exception)
        {
            // Диалог отменён/недоступен — молча, чтобы не ронять UI.
        }
    }
}
