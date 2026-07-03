using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Backup;

/// <summary>Хранит последний снимок состояния системы (JSON в %LOCALAPPDATA%\Aegis) для функции «Что изменилось».</summary>
public sealed class SystemSnapshotStore : ISystemSnapshotStore
{
    private readonly string _filePath;

    /// <param name="fileName">Имя файла снимка — разные функции («Что изменилось» и автозапуск) хранят свои базы отдельно.</param>
    public SystemSnapshotStore(string fileName = "system-snapshot.json")
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", fileName);
    }

    public SystemSnapshot? LoadLatest()
    {
        try
        {
            return File.Exists(_filePath)
                ? JsonSerializer.Deserialize<SystemSnapshot>(File.ReadAllText(_filePath))
                : null;
        }
        catch (Exception)
        {
            return null; // битый/недоступный файл — считаем, что снимка нет
        }
    }

    public void Save(SystemSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception)
        {
            // Не смогли сохранить — не критично (в следующий раз просто не будет сравнения).
        }
    }
}
