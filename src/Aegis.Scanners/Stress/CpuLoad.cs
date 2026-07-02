namespace Aegis.Scanners.Stress;

/// <summary>
/// Реальная нагрузка на процессор: на каждом ядре — выделенный фоновый поток с тяжёлым счётом в цикле.
/// Приоритет ниже обычного (<see cref="ThreadPriority.BelowNormal"/>), чтобы интерфейс приложения
/// (кнопка «Стоп», живая шкала) оставался отзывчивым, но ядра при этом полностью загружены и греются.
/// Используем именно <see cref="Thread"/>, а не пул задач: занятый цикл не должен забивать пул, в котором
/// крутятся асинхронные шаги движка.
/// </summary>
public sealed class CpuLoad : ICpuLoad
{
    public IDisposable Start(int? threadCount = null)
    {
        var count = Math.Max(1, threadCount ?? Environment.ProcessorCount);
        var cts = new CancellationTokenSource();
        var threads = new Thread[count];
        for (var i = 0; i < count; i++)
        {
            threads[i] = new Thread(() => Burn(cts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal,
                Name = "AegisStress-" + i,
            };
            threads[i].Start();
        }

        return new Session(cts, threads);
    }

    /// <summary>Тяжёлый счёт без аллокаций — держит ядро занятым, пока не попросят остановиться.</summary>
    private static void Burn(CancellationToken cancellationToken)
    {
        var x = 1.0001;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Десятки тысяч операций между проверками флага — чтобы проверка не «съедала» нагрузку.
            for (var i = 0; i < 20_000; i++)
            {
                x = Math.Sqrt(x * 1.0000001) + Math.Sin(x);
                if (x > 1e6)
                {
                    x = 1.0001;
                }
            }
        }
    }

    private sealed class Session : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly Thread[] _threads;
        private bool _stopped;

        public Session(CancellationTokenSource cts, Thread[] threads)
        {
            _cts = cts;
            _threads = threads;
        }

        public void Dispose()
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _cts.Cancel();
            foreach (var thread in _threads)
            {
                thread.Join(TimeSpan.FromSeconds(2));
            }

            _cts.Dispose();
        }
    }
}
