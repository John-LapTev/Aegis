using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Aegis.App.Converters;

/// <summary>
/// Имя ИИ-модели → картинка-логотип (PNG-ресурс) или null, если у модели нет картинки (тогда рисуется
/// SVG-знак из <see cref="ModelIconConverter"/>). Сейчас картинка есть только у Gemini (цветная искра).
/// </summary>
public sealed class ModelImageConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Assets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gemini"] = "avares://Aegis/Assets/models/gemini.png",
    };

    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key))
        {
            key = "Gemini"; // как и SVG-знак, по умолчанию (модель ещё не определена) — Gemini
        }

        if (!Assets.TryGetValue(key, out var uri))
        {
            return null; // у Groq/Mistral картинки нет → рисуется SVG-знак
        }

        if (!Cache.TryGetValue(key, out var bitmap))
        {
            try
            {
                bitmap = new Bitmap(AssetLoader.Open(new Uri(uri)));
            }
            catch (Exception)
            {
                bitmap = null;
            }

            Cache[key] = bitmap;
        }

        return bitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
