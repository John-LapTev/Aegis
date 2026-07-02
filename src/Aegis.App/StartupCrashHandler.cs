using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Aegis.App;

/// <summary>
/// Делает любые необработанные ошибки видимыми. Без него GUI-приложение (<c>WinExe</c>) при сбое на
/// запуске просто «молча» исчезает — нет ни окна, ни ошибки, ни процесса в диспетчере. Перехватчик
/// пишет понятный отчёт в файл (<c>%LocalAppData%\Aegis\logs\crash-*.txt</c>) и показывает окно с текстом
/// ошибки, чтобы её можно было прочитать и переслать.
/// </summary>
internal static class StartupCrashHandler
{
    /// <summary>Подписаться на ошибки, которые не поймал <c>try/catch</c> (в т.ч. в фоновых потоках).</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        // Незамеченные исключения фоновых задач (fire-and-forget, оборванные await) — тихо логируем
        // и помечаем обработанными, чтобы диагностика не терялась, но и модалка на каждую фоновую
        // ошибку не выскакивала. Настоящие краши команд закрыты локальными try/catch в VM.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            TryLog(e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Зафиксировать ошибку: лог-файл рядом с обычными логами, запись в Serilog и окно с текстом.</summary>
    public static void Report(Exception? exception)
    {
        var text = Describe(exception);
        TryWriteFile(text);
        TryLog(exception);
        TryShowDialog(text);
    }

    private static string Describe(Exception? exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Aegis не смог запуститься из-за ошибки.");
        builder.AppendLine("Время: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("Версия: " + (typeof(StartupCrashHandler).Assembly.GetName().Version?.ToString() ?? "?"));
        builder.AppendLine();
        builder.AppendLine("Что произошло (это можно переслать разработчику):");
        builder.AppendLine(exception?.ToString() ?? "Неизвестная ошибка — данных об исключении нет.");
        return builder.ToString();
    }

    private static void TryWriteFile(string text)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "logs");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(file, text);
        }
        catch (Exception)
        {
            // В обработчике аварий падать нельзя — если запись не удалась, просто молчим.
        }
    }

    private static void TryLog(Exception? exception)
    {
        try
        {
            Serilog.Log.Fatal(exception, "Необработанная ошибка — приложение аварийно завершилось.");
            Serilog.Log.CloseAndFlush();
        }
        catch (Exception)
        {
            // Логгер мог быть ещё не настроен (сбой до инициализации) — пропускаем.
        }
    }

    private static void TryShowDialog(string text)
    {
        try
        {
            // MB_ICONERROR (0x10): красная иконка ошибки. hWnd=Zero — окно поверх всех, не привязано к UI-потоку.
            MessageBoxW(IntPtr.Zero, text, "Aegis — ошибка запуска", 0x10);
        }
        catch (Exception)
        {
            // Не на Windows или user32 недоступен — отчёт уже записан в файл.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
