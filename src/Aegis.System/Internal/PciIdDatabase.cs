using System.Reflection;

namespace Aegis.System.Internal;

/// <summary>
/// База PCI-ID (pci-ids.ucw.cz, лицензия BSD/GPL, встроена как ресурс): по VEN даёт имя производителя,
/// по VEN+DEV — модель устройства. Нужна, чтобы называть PCI-железо (видеокарты, сетевые/звуковые контроллеры)
/// — дополняет usb.ids для USB. Формат файла такой же, как у usb.ids. Парсер чистый; файл грузится лениво один раз.
/// </summary>
internal sealed class PciIdDatabase
{
    private readonly Dictionary<string, string> _vendors;
    private readonly Dictionary<string, string> _devices;

    private PciIdDatabase(Dictionary<string, string> vendors, Dictionary<string, string> devices)
    {
        _vendors = vendors;
        _devices = devices;
    }

    private static readonly Lazy<PciIdDatabase> LazyInstance = new(Load);

    public static PciIdDatabase Instance => LazyInstance.Value;

    public int VendorCount => _vendors.Count;

    /// <summary>Имя производителя по VEN (4 hex) или null.</summary>
    public string? VendorName(string? ven) =>
        ven is { Length: 4 } ? _vendors.GetValueOrDefault(ven.ToLowerInvariant()) : null;

    /// <summary>Имя устройства по VEN+DEV (по 4 hex) или null.</summary>
    public string? DeviceName(string? ven, string? dev) =>
        ven is { Length: 4 } && dev is { Length: 4 }
            ? _devices.GetValueOrDefault($"{ven.ToLowerInvariant()}:{dev.ToLowerInvariant()}")
            : null;

    /// <summary>Разобрать содержимое pci.ids (чистая функция — для тестов и для загрузки ресурса).</summary>
    public static PciIdDatabase Parse(string content)
    {
        var vendors = new Dictionary<string, string>(StringComparer.Ordinal);
        var devices = new Dictionary<string, string>(StringComparer.Ordinal);
        string? currentVendor = null;

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
                    currentVendor = line[..4].ToLowerInvariant();
                    vendors[currentVendor] = line[6..].Trim();
                }
                else
                {
                    currentVendor = null;
                }
            }
            else if (line[1] != '\t' && currentVendor is not null
                     && IsHex4(line, 1) && line[5] == ' ' && line[6] == ' ')
            {
                // Строка устройства: "\tdddd  name".
                devices[$"{currentVendor}:{line[1..5].ToLowerInvariant()}"] = line[7..].Trim();
            }
            // Строки с двумя табами (подсистемы) пропускаем.
        }

        return new PciIdDatabase(vendors, devices);
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

    private static PciIdDatabase Load()
    {
        try
        {
            var assembly = typeof(PciIdDatabase).Assembly;
            var name = Array.Find(assembly.GetManifestResourceNames(),
                n => n.EndsWith("pci.ids", StringComparison.OrdinalIgnoreCase));
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
