using System.Reflection;
using System.Text.Json;

namespace Aegis.System.Internal;

/// <summary>
/// База соответствий «название видеокарты NVIDIA → pfid» (ZenitH-AT/nvidia-data, встроена как ресурс). Нужна, чтобы
/// спросить у скрытого сервиса NVIDIA последнюю версию драйвера. Имена нормализуем (убираем «NVIDIA», «Laptop GPU»).
/// </summary>
internal sealed class NvidiaGpuData
{
    private readonly Dictionary<string, string> _pfidByName;

    private NvidiaGpuData(Dictionary<string, string> pfidByName) => _pfidByName = pfidByName;

    private static readonly Lazy<NvidiaGpuData> LazyInstance = new(Load);

    public static NvidiaGpuData Instance => LazyInstance.Value;

    public int Count => _pfidByName.Count;

    /// <summary>pfid по названию видеокарты (точное совпадение после нормализации); null — не нашли.</summary>
    public string? FindPfid(string gpuName) =>
        _pfidByName.GetValueOrDefault(Normalize(gpuName));

    // NB: «Laptop GPU» НЕ убираем — ноутбучные и десктопные карты одной модели имеют разные pfid
    // (RTX 3070 = 933, RTX 3070 Laptop GPU = 939). WMI у ноутбуков и пишет «… Laptop GPU».
    private static string Normalize(string name) => name
        .Replace("NVIDIA", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("(R)", string.Empty, StringComparison.Ordinal)
        .Replace("(TM)", string.Empty, StringComparison.Ordinal)
        .Trim()
        .ToLowerInvariant();

    /// <summary>Разобрать gpu-data.json: { "desktop": {"GeForce RTX 3070":"933", …}, "notebook": {…} } (чистая функция).</summary>
    public static NvidiaGpuData Parse(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var section in document.RootElement.EnumerateObject())
            {
                foreach (var gpu in section.Value.EnumerateObject())
                {
                    var key = Normalize(gpu.Name);
                    if (key.Length > 0 && gpu.Value.GetString() is { Length: > 0 } pfid)
                    {
                        map[key] = pfid;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Битый ресурс — просто пустая база (проверка обновлений тихо отключится).
        }

        return new NvidiaGpuData(map);
    }

    private static NvidiaGpuData Load()
    {
        try
        {
            var assembly = typeof(NvidiaGpuData).Assembly;
            var name = Array.Find(assembly.GetManifestResourceNames(),
                n => n.EndsWith("nvidia-gpu-data.json", StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                return Parse("{}");
            }

            using var stream = assembly.GetManifestResourceStream(name);
            using var reader = new StreamReader(stream!);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception)
        {
            return Parse("{}");
        }
    }
}
