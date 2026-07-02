using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Aegis.App.Views;

/// <summary>
/// Окно «всё работает после изменений?» с обратным отсчётом. Если пользователь не подтвердит «всё ок» за
/// отведённое время — сделанные правки откатываются автоматически (защита на случай, когда система сломалась и
/// человек не может нажать). «Откат» вызывает переданный <c>onRollback</c> (возврат правок по их бэкапам).
/// </summary>
public partial class RollbackConfirmWindow : Window
{
    private const int CountdownSeconds = 300; // 5 минут, иначе авто-откат

    private readonly DispatcherTimer _timer;
    private readonly Func<Task>? _onRollback;
    private readonly Action? _onKeep;
    private int _remaining = CountdownSeconds;
    private bool _handled;

    public RollbackConfirmWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public RollbackConfirmWindow(string description, Func<Task> onRollback, Action onKeep) : this()
    {
        _onRollback = onRollback;
        _onKeep = onKeep;
        MessageText.Text =
            $"Только что применены изменения в системе ({description}). Их можно отменить. " +
            "Убедись, что компьютер работает нормально, и нажми «Да, всё работает». Если что-то сломалось — «Откатить».";
        UpdateCountdown();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;
        if (_remaining <= 0)
        {
            _ = RollbackAsync(); // никто не подтвердил — откатываем
            return;
        }

        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        var minutes = _remaining / 60;
        var seconds = _remaining % 60;
        CountdownText.Text =
            $"Если не подтвердить, изменения автоматически откатятся через {minutes:0}:{seconds:00} — на случай, " +
            "если система перестала отвечать.";
    }

    private void OnKeep(object? sender, RoutedEventArgs e)
    {
        if (_handled)
        {
            return;
        }

        _handled = true;
        _timer.Stop();
        _onKeep?.Invoke();
        Close();
    }

    private void OnRollback(object? sender, RoutedEventArgs e) => _ = RollbackAsync();

    private async Task RollbackAsync()
    {
        if (_handled)
        {
            return;
        }

        _handled = true;
        _timer.Stop();
        CountdownText.Text = "Отменяю сделанные изменения…";
        try
        {
            if (_onRollback is not null)
            {
                await _onRollback().ConfigureAwait(true);
            }
        }
        catch (Exception)
        {
            // ignore — откат best-effort
        }

        Close();
    }
}
