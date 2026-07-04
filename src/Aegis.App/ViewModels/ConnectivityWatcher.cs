using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Aegis.App.ViewModels;

/// <summary>
/// Следит за наличием интернета (быстрый TCP-пинг к надёжному узлу + периодический таймер + событие смены сети) и
/// сообщает статус колбэком. Механика вынесена из MainWindowViewModel: подключили кабель — плашка «нет интернета»
/// сама исчезает (и наоборот), без перезапуска. <see cref="IDisposable"/> — отписка от статического события и стоп таймера.
/// </summary>
public sealed class ConnectivityWatcher : IDisposable
{
    private readonly Action<bool> _onOnlineChanged; // true = есть интернет
    private readonly DispatcherTimer _timer;
    private readonly NetworkAddressChangedEventHandler _networkChanged;
    private bool _started;

    public ConnectivityWatcher(Action<bool> onOnlineChanged)
    {
        ArgumentNullException.ThrowIfNull(onOnlineChanged);
        _onOnlineChanged = onOnlineChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _timer.Tick += (_, _) => _ = CheckAsync();
        _networkChanged = (_, _) => Dispatcher.UIThread.Post(() => _ = CheckAsync());
    }

    /// <summary>Первая проверка + запуск слежения (таймер + событие смены сетевого адреса). Повторный вызов игнорируется.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _ = CheckAsync();
        _timer.Start();
        NetworkChange.NetworkAddressChanged += _networkChanged;
    }

    private async Task CheckAsync()
    {
        bool online;
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync("1.1.1.1", 443, timeout.Token).ConfigureAwait(true);
            online = client.Connected;
        }
        catch (Exception)
        {
            online = false; // нет сети / таймаут / отказ — считаем офлайн
        }

        _onOnlineChanged(online);
    }

    public void Dispose()
    {
        if (_started)
        {
            NetworkChange.NetworkAddressChanged -= _networkChanged;
            _started = false;
        }

        _timer.Stop();
    }
}
