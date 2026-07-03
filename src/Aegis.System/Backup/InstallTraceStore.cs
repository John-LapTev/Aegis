using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Backup;

/// <summary>Хранит «следы установки» программ (JSON в %LOCALAPPDATA%\Aegis) для полного удаления в духе Revo.</summary>
public sealed class InstallTraceStore : IInstallTraceStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "install-traces.json");

    private readonly object _gate = new();

    public IReadOnlyList<InstallTrace> LoadAll()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<InstallTrace>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (Exception)
        {
            return []; // битый/недоступный файл — считаем, что следов нет
        }
    }

    public InstallTrace? Find(string programName) =>
        LoadAll().FirstOrDefault(t => string.Equals(t.ProgramName, programName, StringComparison.OrdinalIgnoreCase));

    public void Save(InstallTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        lock (_gate)
        {
            var traces = LoadAll()
                .Where(t => !string.Equals(t.ProgramName, trace.ProgramName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            traces.Add(trace);
            Write(traces);
        }
    }

    public void Remove(string programName)
    {
        if (string.IsNullOrWhiteSpace(programName))
        {
            return;
        }

        lock (_gate)
        {
            var traces = LoadAll()
                .Where(t => !string.Equals(t.ProgramName, programName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Write(traces);
        }
    }

    private static void Write(List<InstallTrace> traces)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(traces));
        }
        catch (Exception)
        {
            // Не смогли сохранить — не критично (просто не будет следа для полного удаления).
        }
    }
}
