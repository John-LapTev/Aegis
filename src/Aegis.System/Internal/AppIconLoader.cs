using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using Aegis.Core.Abstractions;

namespace Aegis.System.Internal;

/// <summary>
/// Извлекает значок программы из строки DisplayIcon (реестр): «C:\App\app.exe,0», «C:\App\app.exe» или «…\icon.ico».
/// Возвращает PNG-байты (UI сам сделает из них картинку). Всё best-effort — при любой ошибке возвращает null.
/// </summary>
public sealed class AppIconLoader : IAppIconLoader
{
    public byte[]? LoadPng(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var (path, index) = ParseIconPath(iconPath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var large = new IntPtr[1];
            var extracted = ExtractIconEx(path, index, large, Array.Empty<IntPtr>(), 1);
            var handle = extracted > 0 ? large[0] : IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using var icon = Icon.FromHandle(handle);
                using var bitmap = icon.ToBitmap();
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch (Exception)
        {
            return null; // не смогли извлечь — покажем заглушку
        }
    }

    /// <summary>Разбирает «путь,индекс» (индекс необязателен, по умолчанию 0); убирает кавычки.</summary>
    private static (string Path, int Index) ParseIconPath(string raw)
    {
        raw = raw.Trim().Trim('"');
        var comma = raw.LastIndexOf(',');
        if (comma > 1 && int.TryParse(raw[(comma + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return (raw[..comma].Trim().Trim('"'), index);
        }

        return (raw, 0);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[] largeIcons, IntPtr[] smallIcons, uint count);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
