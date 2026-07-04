using System;
using System.Diagnostics;

namespace Aegis.App.ViewModels;

/// <summary>Открытие файлов/папок/ссылок во внешних программах (Проводник, браузер). Возвращает текст ошибки или null.</summary>
public static class ExternalOpener
{
    /// <summary>Открыть файл/папку/ссылку в системной программе по умолчанию.</summary>
    public static string? Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Открыть Проводник с выделенным файлом.</summary>
    public static string? RevealInExplorer(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true,
            });
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
