using System.Reflection;

namespace Aegis.System.Internal;

/// <summary>
/// База USB-ID (linux-usb.org, встроена как ресурс): по коду VID даёт имя производителя, по VID+PID — модель.
/// Нужна, чтобы называть подключённые USB-устройства (мыши/клавиатуры/донглы) и опознавать вендора для
/// подбора фирменной утилиты. Парсер чистый (тестируется); файл грузится лениво один раз.
/// </summary>
internal sealed class UsbIdDatabase
{
    private readonly Dictionary<string, string> _vendors;
    private readonly Dictionary<string, string> _products;

    private UsbIdDatabase(Dictionary<string, string> vendors, Dictionary<string, string> products)
    {
        _vendors = vendors;
        _products = products;
    }

    private static readonly Lazy<UsbIdDatabase> LazyInstance = new(Load);

    public static UsbIdDatabase Instance => LazyInstance.Value;

    public int VendorCount => _vendors.Count;

    /// <summary>Имя производителя по VID (4 hex) или null.</summary>
    public string? VendorName(string? vid) =>
        vid is { Length: 4 } ? _vendors.GetValueOrDefault(vid.ToLowerInvariant()) : null;

    /// <summary>Имя модели по VID+PID (по 4 hex) или null.</summary>
    public string? ProductName(string? vid, string? pid) =>
        vid is { Length: 4 } && pid is { Length: 4 }
            ? _products.GetValueOrDefault($"{vid.ToLowerInvariant()}:{pid.ToLowerInvariant()}")
            : null;

    /// <summary>Разобрать содержимое usb.ids (чистая функция — для тестов и для загрузки ресурса).</summary>
    public static UsbIdDatabase Parse(string content)
    {
        var vendors = new Dictionary<string, string>(StringComparer.Ordinal);
        var products = new Dictionary<string, string>(StringComparer.Ordinal);
        string? currentVid = null;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 7 || line[0] == '#')
            {
                continue;
            }

            if (line[0] != '\t')
            {
                // Строка производителя: "vvvv  name" (4 hex, два пробела). Иначе — другая секция (классы и т.п.).
                if (IsHex4(line, 0) && line[4] == ' ' && line[5] == ' ')
                {
                    currentVid = line[..4].ToLowerInvariant();
                    vendors[currentVid] = line[6..].Trim();
                }
                else
                {
                    currentVid = null;
                }
            }
            else if (line[1] != '\t' && currentVid is not null
                     && IsHex4(line, 1) && line[5] == ' ' && line[6] == ' ')
            {
                // Строка устройства: "\tpppp  name".
                products[$"{currentVid}:{line[1..5].ToLowerInvariant()}"] = line[7..].Trim();
            }
            // Строки с двумя табами (интерфейсы) пропускаем.
        }

        return new UsbIdDatabase(vendors, products);
    }

    private static bool IsHex4(string s, int offset)
    {
        for (var i = offset; i < offset + 4; i++)
        {
            if (!Uri.IsHexDigit(s[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static UsbIdDatabase Load()
    {
        try
        {
            var assembly = typeof(UsbIdDatabase).Assembly;
            var name = Array.Find(assembly.GetManifestResourceNames(),
                n => n.EndsWith("usb.ids", StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                return Parse(string.Empty);
            }

            using var stream = assembly.GetManifestResourceStream(name);
            using var reader = new StreamReader(stream!);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception)
        {
            return Parse(string.Empty);
        }
    }
}
