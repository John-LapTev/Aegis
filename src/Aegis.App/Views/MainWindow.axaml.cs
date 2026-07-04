using Aegis.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aegis.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>Escape в подразделе Дашборда → назад на Дашборд (правка Ивана 1168).</summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm && vm.TryReturnToDashboard())
        {
            e.Handled = true;
        }
    }
}
