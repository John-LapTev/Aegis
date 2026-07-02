using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Aegis.Core.Models;

namespace Aegis.App.Converters;

/// <summary>
/// Преобразует <see cref="Severity"/> в кисть статуса (цвета — токены из docs/DESIGN.md).
/// Параметр <c>"subtle"</c> возвращает приглушённую кисть-подложку (для бейджа).
/// </summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    // Цвета — из единого StatusColors (одно место на C#-стороне).
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse(StatusColors.Ok));
    private static readonly IBrush Warn = new SolidColorBrush(Color.Parse(StatusColors.Warn));
    private static readonly IBrush Danger = new SolidColorBrush(Color.Parse(StatusColors.Danger));
    private static readonly IBrush Info = new SolidColorBrush(Color.Parse(StatusColors.Info));

    private static readonly IBrush OkSubtle = new SolidColorBrush(Color.Parse(StatusColors.Subtle(StatusColors.Ok)));
    private static readonly IBrush WarnSubtle = new SolidColorBrush(Color.Parse(StatusColors.Subtle(StatusColors.Warn)));
    private static readonly IBrush DangerSubtle = new SolidColorBrush(Color.Parse(StatusColors.Subtle(StatusColors.Danger)));
    private static readonly IBrush InfoSubtle = new SolidColorBrush(Color.Parse(StatusColors.Subtle(StatusColors.Info)));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Severity severity)
        {
            return Brushes.Transparent;
        }

        var subtle = parameter as string == "subtle";
        return severity switch
        {
            Severity.Ok => subtle ? OkSubtle : Ok,
            Severity.Info => subtle ? InfoSubtle : Info,
            Severity.Warning => subtle ? WarnSubtle : Warn,
            Severity.Danger => subtle ? DangerSubtle : Danger,
            _ => Brushes.Gray,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
