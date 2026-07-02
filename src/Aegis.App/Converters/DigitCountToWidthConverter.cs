using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Aegis.App.Converters;

/// <summary>
/// Ширина флажка по числу цифр в значении: 1 цифра — узкий, 2–3 — шире. Так флажок «расширяется» под число
/// надёжно, без опоры на авто-размер скруглённого Border (который на Windows не растягивается и режет цифры).
/// </summary>
public sealed class DigitCountToWidthConverter : IValueConverter
{
    private const double Base = 13d;      // базовая ширина (краевые отступы)
    private const double PerDigit = 9d;   // прибавка на каждую цифру

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var digits = value is int count ? Math.Abs(count).ToString(CultureInfo.InvariantCulture).Length : 1;
        return Base + Math.Clamp(digits, 1, 4) * PerDigit;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
