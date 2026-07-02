using System.Globalization;

namespace Aegis.Core;

/// <summary>
/// Единое форматирование размера в байтах в понятную строку («2.1 ГБ», «512 КБ»). Одно место на весь проект,
/// чтобы размеры в результатах сканов и в UI выглядели одинаково.
/// </summary>
public static class HumanSize
{
    private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ", "ТБ", "ПБ"];

    public static string Format(long bytes)
    {
        var size = bytes < 0 ? 0d : bytes;
        var unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        // Б и КБ — целым числом, от МБ и выше — с одним знаком после запятой.
        var value = unit <= 1
            ? Math.Round(size).ToString(CultureInfo.InvariantCulture)
            : size.ToString("0.0", CultureInfo.InvariantCulture);

        return $"{value} {Units[unit]}";
    }
}
