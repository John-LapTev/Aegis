using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace Aegis.System.Internal;

/// <summary>
/// Преобразование значения реестра ↔ строка для JSON-бэкапа с сохранением типа
/// (DWord/QWord/Binary/MultiString/String). Раньше всё приводилось к строке через <c>ToString()</c>,
/// из-за чего Binary/MultiString теряли данные уже при бэкапе, а откат портил не-DWord значения.
/// Чистые функции (без обращения к реестру) — тестируются на любой ОС.
/// </summary>
internal static class RegistryValueCodec
{
    /// <summary>Закодировать значение реестра в строку для хранения в бэкапе.</summary>
    public static string Encode(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
        RegistryValueKind.MultiString => JsonSerializer.Serialize((string[])value),
        _ => value?.ToString() ?? string.Empty,
    };

    /// <summary>Декодировать строку бэкапа обратно в значение реестра нужного типа.</summary>
    public static object Decode(string? value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0,
        RegistryValueKind.QWord => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : 0L,
        RegistryValueKind.Binary => DecodeBinary(value),
        RegistryValueKind.MultiString => DecodeMultiString(value),
        _ => value ?? string.Empty,
    };

    private static byte[] DecodeBinary(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private static string[] DecodeMultiString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
