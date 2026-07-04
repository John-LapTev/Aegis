using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Backup;

/// <summary>
/// Хранит накопленную статистику действий Aegis (JSON в %LOCALAPPDATA%\Aegis) для раздела «Сравнить состояние».
/// Счётчики только растут; ошибки чтения/записи гасятся — статистика не критична для работы программы.
/// </summary>
public sealed class ActivityStatsStore : IActivityStatsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "activity-stats.json");

    private readonly object _gate = new();

    public ActivityStats Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<ActivityStats>(File.ReadAllText(FilePath)) ?? new ActivityStats()
                : new ActivityStats();
        }
        catch (Exception)
        {
            return new ActivityStats(); // битый/недоступный файл — считаем, что статистики нет
        }
    }

    public void AddJunkCleaned(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        Update(current => current with { JunkCleanedBytes = current.JunkCleanedBytes + bytes });
    }

    public void AddDriversUpdated(int count = 1) =>
        Update(current => current with { DriversUpdated = current.DriversUpdated + Math.Max(0, count) });

    public void AddProgramsRemoved(int count = 1) =>
        Update(current => current with { ProgramsRemoved = current.ProgramsRemoved + Math.Max(0, count) });

    public void AddThreatsNeutralized(int count = 1) =>
        Update(current => current with { ThreatsNeutralized = current.ThreatsNeutralized + Math.Max(0, count) });

    private void Update(Func<ActivityStats, ActivityStats> change)
    {
        lock (_gate)
        {
            var updated = change(Load());
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(updated));
            }
            catch (Exception)
            {
                // Не смогли сохранить — не критично (статистика просто не обновится).
            }
        }
    }
}
