using System.Threading;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Guard;
using Aegis.Scanners.Probing;

namespace Aegis.System.Guard;

/// <summary>
/// Тихий фоновый страж: раз в несколько минут снимает список процессов и простой пользователя, прогоняет через
/// <see cref="GuardEvaluator"/> и поднимает уведомление о «живых» угрозах (в первую очередь скрытых майнерах).
/// Об одном и том же не сообщает повторно. Ошибки фоновой проверки гасит тихо — страж не должен ронять приложение.
/// </summary>
public sealed class SystemGuard : ISystemGuard, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);

    private readonly IProcessProbe _processProbe;
    private readonly IUserActivityProbe _activityProbe;
    private readonly GuardEvaluator _evaluator = new();
    private readonly HashSet<string> _alerted = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private Timer? _timer;
    private int _checking; // 0/1 через Interlocked — атомарно, без гонки между тиками таймера

    public SystemGuard(IProcessProbe processProbe, IUserActivityProbe activityProbe)
    {
        ArgumentNullException.ThrowIfNull(processProbe);
        ArgumentNullException.ThrowIfNull(activityProbe);
        _processProbe = processProbe;
        _activityProbe = activityProbe;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler<GuardAlert>? AlertRaised;

    public void Start()
    {
        lock (_gate)
        {
            if (IsRunning)
            {
                return;
            }

            IsRunning = true;
            _timer = new Timer(static state => _ = ((SystemGuard)state!).TickAsync(), this, FirstDelay, Interval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private async Task TickAsync()
    {
        if (Interlocked.Exchange(ref _checking, 1) == 1)
        {
            return; // предыдущая проверка ещё идёт — не накладываем (атомарно)
        }

        try
        {
            var processes = await _processProbe.FindAsync().ConfigureAwait(false);
            var idle = _activityProbe.GetIdleDuration();

            foreach (var alert in _evaluator.Evaluate(processes, idle))
            {
                bool isNew;
                lock (_gate)
                {
                    isNew = _alerted.Add(alert.Key);
                }

                if (isNew)
                {
                    AlertRaised?.Invoke(this, alert);
                }
            }
        }
        catch (Exception)
        {
            // Фоновая проверка не должна падать шумно — тихо пропускаем этот цикл.
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    public void Dispose() => Stop();
}
