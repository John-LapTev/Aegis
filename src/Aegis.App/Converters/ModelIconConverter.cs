using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aegis.App.Converters;

/// <summary>
/// Имя ИИ-модели («Gemini»/«Groq»/«Mistral») → её значок (залитый SVG-путь, 24×24). Значок в шапке меняется
/// под активную модель цепочки. По умолчанию (пусто/неизвестно) — значок-искра Gemini.
/// </summary>
public sealed class ModelIconConverter : IValueConverter
{
    // Официальные логотипы, перерисованные в SVG (монохром, viewbox 24×24), залив их фирменным цветом:
    // Gemini и Mistral — точные пути (Simple Icons). У Groq нет иконки-логотипа (только текстовый), поэтому
    // чистый знак-молния (их фишка — скорость).
    private static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gemini"] = "M11.04 19.32Q12 21.51 12 24q0-2.49.93-4.68.96-2.19 2.58-3.81t3.81-2.55Q21.51 12 24 12q-2.49 0-4.68-.93a12.3 12.3 0 0 1-3.81-2.58 12.3 12.3 0 0 1-2.58-3.81Q12 2.49 12 0q0 2.49-.96 4.68-.93 2.19-2.55 3.81a12.3 12.3 0 0 1-3.81 2.58Q2.49 12 0 12q2.49 0 4.68.96 2.19.93 3.81 2.55t2.55 3.81",
        ["Mistral"] = "M17.143 3.429v3.428h-3.429v3.429h-3.428V6.857H6.857V3.43H3.43v13.714H0v3.428h10.286v-3.428H6.857v-3.429h3.429v3.429h3.429v-3.429h3.428v3.429h-3.428v3.428H24v-3.428h-3.43V3.429z",
        ["Groq"] = "M13 2 L4 13 H11 L9 22 L20 10 H13 Z",
        // Поисковые сервисы — универсальный знак «лупа» (поиск), в фирменных цветах через ModelBrush.
        ["Tavily"] = "M10 2a8 8 0 1 0 4.9 14.32l5.39 5.39 1.42-1.42-5.39-5.39A8 8 0 0 0 10 2zm0 2a6 6 0 1 1 0 12 6 6 0 0 1 0-12z",
        ["Serper"] = "M10 2a8 8 0 1 0 4.9 14.32l5.39 5.39 1.42-1.42-5.39-5.39A8 8 0 0 0 10 2zm0 2a6 6 0 1 1 0 12 6 6 0 0 1 0-12z",
        // ChatGPT и GPT-4o — официальный логотип OpenAI («цветок»-узел, Simple Icons), правка 933. Цвет — через ModelBrush.
        ["ChatGPT"] = OpenAiLogo,
        ["GPT-4o"] = OpenAiLogo,
        // Claude (Anthropic) — фирменный «сан-бёрст» (звезда-искра). Цвет — глиняно-оранжевый через ModelBrush.
        ["Claude"] = ClaudeBurst,
    };

    /// <summary>Знак Claude/Anthropic — восьмилучевая искра-бёрст (viewBox 24×24, залитая).</summary>
    private const string ClaudeBurst =
        "M22 12 L15.23 13.34 L19.07 19.07 L13.34 15.23 L12 22 L10.66 15.23 L4.93 19.07 L8.77 13.34 " +
        "L2 12 L8.77 10.66 L4.93 4.93 L10.66 8.77 L12 2 L13.34 8.77 L19.07 4.93 L15.23 10.66 Z";

    /// <summary>Официальный логотип OpenAI (Simple Icons, viewBox 24×24) — для ChatGPT/GPT-4o.</summary>
    private const string OpenAiLogo =
        "M22.2819 9.8211a5.9847 5.9847 0 0 0-.5157-4.9108 6.0462 6.0462 0 0 0-6.5098-2.9A6.0651 6.0651 0 0 0 " +
        "4.9807 4.1818a5.9847 5.9847 0 0 0-3.9977 2.9 6.0462 6.0462 0 0 0 .7427 7.0966 5.98 5.98 0 0 0 .511 " +
        "4.9107 6.051 6.051 0 0 0 6.5146 2.9001A5.9847 5.9847 0 0 0 13.2599 24a6.0557 6.0557 0 0 0 5.7718-4.2058 " +
        "5.9894 5.9894 0 0 0 3.9977-2.9001 6.0557 6.0557 0 0 0-.7475-7.0729zm-9.022 12.6081a4.4755 4.4755 0 0 " +
        "1-2.8764-1.0408l.1419-.0804 4.7783-2.7582a.7948.7948 0 0 0 .3927-.6813v-6.7369l2.02 1.1686a.071.071 0 0 " +
        "1 .038.052v5.5826a4.504 4.504 0 0 1-4.4945 4.4944zm-9.6607-4.1254a4.4708 4.4708 0 0 1-.5346-3.0137l.142.0852 " +
        "4.783 2.7582a.7712.7712 0 0 0 .7806 0l5.8428-3.3685v2.3324a.0804.0804 0 0 1-.0332.0615L9.74 19.9502a4.4992 " +
        "4.4992 0 0 1-6.1408-1.6464zM2.3408 7.8956a4.485 4.485 0 0 1 2.3655-1.9728V11.6a.7664.7664 0 0 0 .3879.6765l5.8144 " +
        "3.3543-2.0201 1.1685a.0757.0757 0 0 1-.071 0l-4.8303-2.7865A4.504 4.504 0 0 1 2.3408 7.872zm16.5963 " +
        "3.8558L13.1038 8.364 15.1192 7.2a.0757.0757 0 0 1 .071 0l4.8303 2.7913a4.4944 4.4944 0 0 1-.6765 " +
        "8.1042v-5.6772a.79.79 0 0 0-.407-.667zm2.0107-3.0231l-.142-.0852-4.7735-2.7818a.7759.7759 0 0 " +
        "0-.7854 0L9.409 9.2297V6.8974a.0662.0662 0 0 1 .0284-.0615l4.8303-2.7866a4.4992 4.4992 0 0 1 6.6802 " +
        "4.66zM8.3065 12.863l-2.02-1.1638a.0804.0804 0 0 1-.038-.0567V6.0742a4.4992 4.4992 0 0 1 7.3757-3.4537l-.142.0805L8.704 " +
        "5.459a.7948.7948 0 0 0-.3927.6813zm1.0976-2.3654l2.602-1.4998 2.6069 1.4998v2.9994l-2.5974 1.4997-2.6067-1.4997Z";

    private static readonly Dictionary<string, Geometry> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key) || !Paths.ContainsKey(key))
        {
            key = "Gemini"; // по умолчанию — первая модель цепочки
        }

        if (!Cache.TryGetValue(key, out var geometry))
        {
            geometry = Geometry.Parse(Paths[key]);
            Cache[key] = geometry;
        }

        return geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Имя ИИ-модели → её фирменная заливка: градиенты как у оригиналов (Gemini сине-фиолетовый,
/// Mistral жёлто-красный), Groq — сплошной оранжево-красный. По умолчанию — Gemini.</summary>
public sealed class ModelBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, IBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        string[] known = ["Groq", "Mistral", "Tavily", "Serper", "ChatGPT", "GPT-4o", "Claude"];
        if (string.IsNullOrEmpty(key) || Array.FindIndex(known, k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) < 0)
        {
            key = "Gemini";
        }

        if (!Cache.TryGetValue(key, out var brush))
        {
            brush = Build(key);
            Cache[key] = brush;
        }

        return brush;
    }

    private static IBrush Build(string key) => key switch
    {
        // Mistral — вертикальный градиент жёлтый→оранжевый→красный (как полосы их логотипа).
        "Mistral" => Gradient(0, 0, 0, 1, ("#FFD800", 0), ("#FF8205", 0.5), ("#FF0107", 1)),
        // Groq — фирменный оранжево-красный (сплошной).
        "Groq" => new SolidColorBrush(Color.Parse("#F55036")),
        // Поисковые сервисы — разные цвета лупы, чтобы отличать.
        "Tavily" => new SolidColorBrush(Color.Parse("#19C2A8")),
        "Serper" => new SolidColorBrush(Color.Parse("#4D6BFE")),
        // ChatGPT (бесплатный) — фирменный зелёный OpenAI.
        "ChatGPT" => new SolidColorBrush(Color.Parse("#10A37F")),
        // GPT-4o (платный, apiglue) — премиум-фиолетовый, чтобы отличать от бесплатного.
        "GPT-4o" => new SolidColorBrush(Color.Parse("#7C5CFC")),
        // Claude (платный, apiglue) — фирменный глиняно-оранжевый Anthropic.
        "Claude" => new SolidColorBrush(Color.Parse("#D97757")),
        // Gemini — диагональный сине-фиолетовый градиент.
        _ => Gradient(0, 0, 1, 1, ("#4285F4", 0), ("#9B72CB", 0.5), ("#D96570", 1)),
    };

    private static LinearGradientBrush Gradient(double x1, double y1, double x2, double y2, params (string Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(x1, y1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(x2, y2, RelativeUnit.Relative),
        };
        foreach (var (color, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(color), offset));
        }

        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
