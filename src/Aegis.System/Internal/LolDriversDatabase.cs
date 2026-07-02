using System.Reflection;

namespace Aegis.System.Internal;

/// <summary>
/// База опасных драйверов из проекта LOLDrivers (loldrivers.io, Apache-2.0), встроена как ресурс в компактном виде
/// «sha256 \t M|V \t имя» (M — подтверждённый вредонос, V — уязвимый, используется в атаках BYOVD). По SHA-256
/// загруженного драйвера говорит, опасен ли он. Парсер чистый (тестируется); файл грузится лениво один раз.
/// </summary>
internal sealed class LolDriversDatabase
{
    public readonly record struct Entry(bool Malicious, string Name);

    private readonly Dictionary<string, Entry> _byHash;

    private LolDriversDatabase(Dictionary<string, Entry> byHash) => _byHash = byHash;

    private static readonly Lazy<LolDriversDatabase> LazyInstance = new(Load);

    public static LolDriversDatabase Instance => LazyInstance.Value;

    public int Count => _byHash.Count;

    /// <summary>Опасен ли драйвер по SHA-256 (64 hex): вернёт запись (вредонос/уязвимый + имя) или null.</summary>
    public Entry? Lookup(string? sha256) =>
        sha256 is { Length: 64 } && _byHash.TryGetValue(sha256.ToLowerInvariant(), out var entry)
            ? entry
            : null;

    /// <summary>Разобрать компактный список «sha256 \t M|V \t имя» (чистая функция — для тестов и загрузки ресурса).</summary>
    public static LolDriversDatabase Parse(string content)
    {
        var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 64)
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length >= 2 && parts[0].Length == 64)
            {
                var name = parts.Length >= 3 ? parts[2] : string.Empty;
                map[parts[0].ToLowerInvariant()] = new Entry(parts[1] == "M", name);
            }
        }

        return new LolDriversDatabase(map);
    }

    private static LolDriversDatabase Load()
    {
        try
        {
            var assembly = typeof(LolDriversDatabase).Assembly;
            var name = Array.Find(assembly.GetManifestResourceNames(),
                n => n.EndsWith("loldrivers.txt", StringComparison.OrdinalIgnoreCase));
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
