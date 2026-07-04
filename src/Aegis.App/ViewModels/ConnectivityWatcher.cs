using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Aegis.App.ViewModels;

/// <summary>
/// Следит за наличием интернета (быстрый TCP-пинг к надёжному узлу + периодический таймер + событие смены сети) и
/// сообщает статус колбэком. Механика вынесена из MainWindowViewModel: подключили кабель — плашка «нет интернета»
/// сама исчезает (и наоборот), без перезапуска.
/// </summary>
public sealed class ConnectivityWatcher
{
    private readonly Action<bool> _onOnlineChanged; // true = есть интернет
    private readonly DispatcherTimer _timer;

    public ConnectivityWatcher(Action<bool> onOnlineChanged)
    {
        ArgumentNullException.ThrowIfNull(onOnlineChanged);
        _onOnlineChanged = onOnlineChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _timer.Tick += (_, _) => _ = CheckAsync();
    }

    /// <summary>Первая проверка + запуск слежения (таймер + событие смены сетевого адреса).</summary>
    public void Start()
    {
        _ = CheckAsync();
        _timer.Start();
        NetworkChange.NetworkAddressChanged += (_, _) => Dispatcher.UIThread.Post(() => _ = CheckAsync());
    }

    private async Task CheckAsync()
    {
        bool online;
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync("1.1.1.1", 443);
            var finished = await Task.WhenAny(connect, Task.Delay(3000)).ConfigureAwait(true);
            online = finished == connect && !connect.IsFaulted && client.Connected;
        }
        catch (Exception)
        {
            online = false;
        }

        _onOnlineChanged(online);
    }
}
