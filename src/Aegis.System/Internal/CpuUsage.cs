namespace Aegis.System.Internal;

/// <summary>
/// Доля загрузки процессора процессом за интервал измерения. Чистая арифметика (без обращения к ОС),
/// поэтому тестируется на любой платформе. Используется эвристикой «возможный майнер» в сканере процессов.
/// </summary>
internal static class CpuUsage
{
    /// <summary>
    /// Загрузка процессора процессом в процентах (0..100 от всей мощности CPU).
    /// </summary>
    /// <param name="cpuDelta">Прирост процессорного времени процесса за интервал.</param>
    /// <param name="elapsedMilliseconds">Реально прошедшее время интервала (мс).</param>
    /// <param name="processorCount">Число логических ядер.</param>
    public static double Percent(TimeSpan cpuDelta, double elapsedMilliseconds, int processorCount)
    {
        if (elapsedMilliseconds <= 0 || processorCount <= 0)
        {
            return 0;
        }

        var percent = cpuDelta.TotalMilliseconds / (elapsedMilliseconds * processorCount) * 100d;
        return Math.Clamp(percent, 0d, 100d);
    }
}
