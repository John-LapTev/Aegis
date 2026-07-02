using System.Text.Json;

namespace Aegis.System.Internal;

/// <summary>
/// Когда последний раз запускали инструмент обслуживания (SFC/DISM, сброс сети). Хранится между запусками
/// в LocalAppData\Aegis\maintenance.json — чтобы показать «запускали недавно (дата)». Best-effort, без падений.
/// </summary>
internal static class MaintenanceHistory
{
    public const string SfcDismKey = "sfc-dism";
    public const string NetworkResetKey = "network-reset";

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "maintenance.json");

    public static DateTimeOffset? GetLastRun(string key)
    {
        try
        {
            return Load().TryGetValue(key, out var value) ? value : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void MarkRun(string key)
    {
        try
        {
            var map = Load();
            map[key] = DateTimeOffset.Now;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(map));
        }
        catch (Exception)
        {
            // Не удалось сохранить — не критично, просто не покажем «запускали недавно».
        }
    }

    private static Dictionary<string, DateTimeOffset> Load()
    {
        if (!File.Exists(FilePath))
        {
            return new Dictionary<string, DateTimeOffset>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(FilePath))
               ?? new Dictionary<string, DateTimeOffset>();
    }
}
