using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aegis.App.Views;

/// <summary>
/// Простое всплывающее окно-результат: заголовок + понятный текст + необязательная кнопка действия
/// (например, «Открыть „Приложения“ Windows»). Возвращает true, если нажали кнопку действия; иначе false.
/// Нужно, чтобы важный итог операции (почему не удалилось / что сделано) не терялся в бледной строке статуса.
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, string? actionLabel = null) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        if (string.IsNullOrEmpty(actionLabel))
        {
            ActionButton.IsVisible = false;
        }
        else
        {
            ActionButton.Content = actionLabel;
        }
    }

    private void OnAction(object? sender, RoutedEventArgs e) => Close(true);

    private void OnClose(object? sender, RoutedEventArgs e) => Close(false);
}
