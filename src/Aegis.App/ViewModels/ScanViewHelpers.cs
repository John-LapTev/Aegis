using System;
using System.IO;
using Aegis.Core.Models;
using Avalonia.Media;

namespace Aegis.App.ViewModels;

/// <summary>
/// Чистые вспомогательные функции экрана сканов (форматирование/парсинг/цвета). Вынесены из
/// <see cref="MainWindowViewModel"/>, чтобы тот не разрастался и отвечал за координацию, а не за мелочи.
/// </summary>
internal static class ScanViewHelpers
{
    /// <summary>Цветная точка-метка по hex-коду (для фильтров и бейджей).</summary>
    public static IBrush Dot(string hex) => new SolidColorBrush(Color.Parse(hex));

    /// <summary>Русское название раздела по группе сканера.</summary>
    public static string GroupTitle(ScanGroup group) => group switch
    {
        ScanGroup.System => "Система",
        ScanGroup.Drivers => "Драйверы",
        ScanGroup.Registry => "Реестр",
        ScanGroup.Autostart => "Автозапуск",
        ScanGroup.Processes => "Процессы",
        ScanGroup.Settings => "Настройки",
        ScanGroup.Junk => "Мусор",
        ScanGroup.Threats => "Угрозы",
        ScanGroup.Missing => "Утилиты",
        ScanGroup.Health => "Здоровье",
        _ => group.ToString(),
    };

    /// <summary>Сколько всего свободно на фиксированных дисках (для отчёта об освобождённом месте).</summary>
    public static long TotalFixedDriveFreeSpace()
    {
        long total = 0;
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive is { DriveType: DriveType.Fixed, IsReady: true })
                    {
                        total += drive.AvailableFreeSpace;
                    }
                }
                catch (Exception)
                {
                    // Диск недоступен — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Не удалось перечислить — 0.
        }

        return total;
    }

    /// <summary>Из строки автозапуска вытащить путь к .exe (отбросив кавычки и аргументы) — для «открыть папку».</summary>
    public static string ExtractExecutablePath(string commandLine)
    {
        var value = commandLine.Trim();
        if (value.Length == 0)
        {
            return value;
        }

        if (value[0] == '"')
        {
            var end = value.IndexOf('"', 1);
            return end > 0 ? value[1..end] : value.Trim('"');
        }

        // Без кавычек путь может содержать пробелы — берём самый длинный существующий префикс (отбрасываем аргументы).
        if (File.Exists(value))
        {
            return value;
        }

        var space = value.IndexOf(' ', StringComparison.Ordinal);
        while (space > 0)
        {
            var candidate = value[..space];
            if (File.Exists(candidate) || File.Exists(candidate + ".exe"))
            {
                return candidate;
            }

            space = value.IndexOf(' ', space + 1);
        }

        var firstSpace = value.IndexOf(' ', StringComparison.Ordinal);
        return firstSpace > 0 ? value[..firstSpace] : value;
    }
}
