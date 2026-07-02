using System.Diagnostics;

namespace Aegis.System.Internal;

/// <summary>Список заведомо лишних задач планировщика (телеметрия/реклама) и их состояние (один вызов schtasks).</summary>
internal static class ScheduledTaskReader
{
    // Имя пути → понятное название. Это известные телеметрия/реклама-задачи Windows (безопасно отключать).
    // ВАЖНО: задачи «Application Experience» (Appraiser/ProgramDataUpdater) Windows защищает (владелец
    // TrustedInstaller) — их нельзя выключить ни schtasks, ни PowerShell. Их сбор данных отключается
    // ШТАТНОЙ политикой реестра AppCompat\DisableInventory — это сделано отдельным переключателем в PrivacyProbe.
    public static readonly (string Path, string Name)[] BloatTasks =
    [
        (@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator", "Программа улучшения качества (Consolidator)"),
        (@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip", "Сбор данных об USB-устройствах"),
        (@"\Microsoft\Windows\Feedback\Siuf\DmClient", "Отправка отзывов в Microsoft"),
        (@"\Microsoft\Windows\Windows Error Reporting\QueueReporting", "Отправка отчётов об ошибках"),
    ];

    /// <summary>Вернуть ВКЛЮЧЁННЫЕ лишние задачи (имя пути → понятное название).</summary>
    public static IReadOnlyList<(string Path, string Name)> GetEnabledBloatTasks()
    {
        // Состояние читаем из XML задачи (отключённая содержит <Enabled>false</Enabled>) — это НЕ зависит
        // от языка Windows. Четыре задачи опрашиваем ПАРАЛЛЕЛЬНО (каждая — свой schtasks), а не по очереди,
        // чтобы не ждать 4 запуска подряд.
        var enabled = new (string Path, string Name)?[BloatTasks.Length];
        Parallel.For(0, BloatTasks.Length, i =>
        {
            var task = BloatTasks[i];
            if (IsTaskEnabled(task.Path))
            {
                enabled[i] = task;
            }
        });

        return enabled.Where(static t => t.HasValue).Select(static t => t!.Value).ToList();
    }

    /// <summary>Задача существует и включена? Состояние — из XML (locale-independent), а не из текстового статуса.</summary>
    private static bool IsTaskEnabled(string path)
    {
        var xml = RunSchtasks($"/query /tn \"{path}\" /xml");
        if (string.IsNullOrWhiteSpace(xml))
        {
            return false; // задачи нет / нет доступа — отключать нечего, не показываем
        }

        // Отключённая задача содержит <Enabled>false</Enabled> в настройках (язык не важен).
        return !xml.Replace(" ", string.Empty, StringComparison.Ordinal)
                   .Contains("<Enabled>false</Enabled>", StringComparison.OrdinalIgnoreCase);
    }

    private static string RunSchtasks(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            // stderr сливаем асинхронно (иначе при выводе в него процесс может зависнуть), stdout читаем целиком.
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginErrorReadLine();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(15000);
            return output;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
