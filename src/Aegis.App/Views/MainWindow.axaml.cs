using Aegis.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aegis.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>При закрытии окна освобождаем VM (наблюдатель сети на статическом событии, токены отмены).</summary>
    protected override void OnClosed(global::System.EventArgs e)
    {
        base.OnClosed(e);
        (DataContext as global::System.IDisposable)?.Dispose();
    }

    /// <summary>
    /// Возвращаемся в окно программы — пересчитываем занимаемое место у показанных пунктов. Человек мог
    /// почистить папку руками в проводнике, и старые цифры вводили бы в заблуждение (запрос Ивана 1353).
    /// </summary>
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (DataContext is MainWindowViewModel vm)
        {
            _ = vm.RefreshVisibleSizesAsync();
        }
    }

    /// <summary>Escape в подразделе Дашборда → назад на Дашборд (правка Ивана 1168).</summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm && vm.TryReturnToDashboard())
        {
            e.Handled = true;
        }
    }
}
