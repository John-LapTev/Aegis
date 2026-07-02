using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Aegis.App.Converters;

/// <summary>Доля 0..1 → угол сектора 0..360° для кольца-заполнения (шкала прогресса по кругу).</summary>
public sealed class FractionToAngleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double fraction ? Math.Clamp(fraction, 0d, 1d) * 360d : 0d;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
