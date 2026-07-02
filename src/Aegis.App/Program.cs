using Avalonia;

namespace Aegis.App;

internal static class Program
{
    // Точка входа. Avalonia требует STA-поток.
    [STAThread]
    public static void Main(string[] args)
    {
        // Любой сбой на запуске (нет нативных библиотек, ошибка XAML и т.п.) делаем видимым,
        // иначе GUI-приложение «молча» закрывается без окна, ошибки и процесса в диспетчере.
        StartupCrashHandler.Install();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            StartupCrashHandler.Report(ex);
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
