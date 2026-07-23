namespace Aegis.Core;

/// <summary>Как часто программа сама проверяет компьютер.</summary>
public enum AutoScanInterval
{
    /// <summary>Не проверять самостоятельно (только по кнопке).</summary>
    Off,

    /// <summary>Раз в неделю.</summary>
    Weekly,

    /// <summary>Раз в месяц.</summary>
    Monthly,
}

/// <summary>
/// Решает, пора ли запускать автоматическую проверку. Отдельный класс с чистой логикой — чтобы правило
/// «пора / не пора» можно было проверить тестами, не дожидаясь недели реального времени.
/// </summary>
public static class AutoScanSchedule
{
    /// <summary>Через сколько дней после прошлой проверки запускать следующую.</summary>
    public static int DaysBetween(AutoScanInterval interval) => interval switch
    {
        AutoScanInterval.Weekly => 7,
        AutoScanInterval.Monthly => 30,
        _ => 0,
    };

    /// <summary>
    /// Пора ли проверять. Проверок «в первый запуск» не устраиваем: если прошлой проверки не было, отсчёт
    /// начинается от текущего момента — иначе программа полезла бы сканировать сразу после установки,
    /// когда человек ещё смотрит на первый экран.
    /// </summary>
    public static bool ShouldRun(AutoScanInterval interval, DateTimeOffset? lastRun, DateTimeOffset now)
    {
        if (interval == AutoScanInterval.Off)
        {
            return false;
        }

        if (lastRun is not DateTimeOffset previous)
        {
            return false;
        }

        // Часы переведены назад / дата в будущем — считаем, что проверять рано (иначе зациклится).
        if (previous > now)
        {
            return false;
        }

        return (now - previous).TotalDays >= DaysBetween(interval);
    }

    /// <summary>Понятная человеку подпись выбранного режима.</summary>
    public static string Describe(AutoScanInterval interval) => interval switch
    {
        AutoScanInterval.Weekly => "Проверять раз в неделю",
        AutoScanInterval.Monthly => "Проверять раз в месяц",
        _ => "Не проверять автоматически",
    };
}
