using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aegis.App.Views;

/// <summary>Выбор пользователя при удалении файлов/папок мусора.</summary>
public enum DeleteChoice
{
    /// <summary>Отменить — ничего не удалять.</summary>
    Cancel,

    /// <summary>Переместить в Корзину Windows (обратимо).</summary>
    Recycle,

    /// <summary>Удалить навсегда (необратимо, освобождает место сразу).</summary>
    Permanent,
}

/// <summary>Диалог «Как удалить?»: Отменить / в Корзину / навсегда. Возвращает <see cref="DeleteChoice"/>.</summary>
public partial class DeleteConfirmWindow : Window
{
    public DeleteConfirmWindow()
    {
        InitializeComponent();
    }

    public DeleteConfirmWindow(string message) : this()
    {
        MessageText.Text = message;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(DeleteChoice.Cancel);

    private void OnRecycle(object? sender, RoutedEventArgs e) => Close(DeleteChoice.Recycle);

    private void OnPermanent(object? sender, RoutedEventArgs e) => Close(DeleteChoice.Permanent);
}
