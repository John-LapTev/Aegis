using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Backup;

/// <summary>
/// Хранит историю здоровья дисков (JSON в %LOCALAPPDATA%\Aegis) для функции «раннее предупреждение по трендам».
/// Держит скользящее окно последних снимков — этого хватает, чтобы видеть динамику, и файл не растёт бесконечно.
/// </summary>
public sealed class HealthTrendStore : IHealthTrendStore
{
    private const int MaxRecords = 90; // ~история проверок; сравниваем текущее с самым старым в окне

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "health-trends.json");

    public IReadOnlyList<HealthTrendSnapshot> LoadHistory()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var history = JsonSerializer.Deserialize<List<HealthTrendSnapshot>>(File.ReadAllText(FilePath));
            return history ?? [];
        }
        catch (Exception)
        {
            return []; // битый/недоступный файл — считаем, что истории нет
        }
    }

    public void Append(HealthTrendSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            var history = LoadHistory().ToList();
            history.Add(snapshot);

            if (history.Count > MaxRecords)
            {
                history.RemoveRange(0, history.Count - MaxRecords);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(history));
        }
        catch (Exception)
        {
            // Не смогли сохранить — не критично (в следующий раз просто не будет части истории).
        }
    }
}
