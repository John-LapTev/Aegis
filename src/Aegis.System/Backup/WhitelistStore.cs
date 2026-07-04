using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.System.Internal;

namespace Aegis.System.Backup;

/// <summary>Белый список с сохранением в %LOCALAPPDATA%\Aegis\whitelist.json. Сравнение путей — без учёта регистра.</summary>
public sealed class WhitelistStore : IWhitelist
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "whitelist.json");

    private readonly HashSet<string> _items;

    public WhitelistStore()
    {
        _items = Load();
    }

    public bool Contains(string key) => !string.IsNullOrWhiteSpace(key) && _items.Contains(key);

    public void Add(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (_items.Add(key))
        {
            Save();
        }
    }

    public void Remove(string key)
    {
        if (!string.IsNullOrWhiteSpace(key) && _items.Remove(key))
        {
            Save();
        }
    }

    private static HashSet<string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var items = JsonSerializer.Deserialize<string[]>(File.ReadAllText(FilePath));
                if (items is not null)
                {
                    return new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception)
        {
            // Битый файл — начинаем с пустого списка.
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(_items.ToArray()));
        }
        catch (Exception)
        {
            // Не удалось сохранить — не критично (в этой сессии всё равно учтётся).
        }
    }
}
