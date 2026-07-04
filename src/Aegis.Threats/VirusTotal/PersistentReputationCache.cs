using System.Text.Json;
using Aegis.Core.Models;

namespace Aegis.Threats.VirusTotal;

/// <summary>
/// Персистентный кэш вердиктов репутации по SHA-256 (файл на диске в %LOCALAPPDATA%\Aegis). Смысл: один и тот
/// же файл (тот же хэш) больше не перепроверяется онлайн при каждом скане и после перезапуска — программа
/// «помнит», что он уже проверен и чист. Если файл ИЗМЕНИЛСЯ, у него другой хэш → запись не найдётся → будет
/// новая проверка (именно так ловится подмена файла по знакомому пути). Вердикты старше окна свежести считаются
/// устаревшими и перепроверяются (на случай, если файл со временем попал в базы как вредоносный).
/// </summary>
public sealed class PersistentReputationCache
{
    private static readonly TimeSpan DefaultFreshness = TimeSpan.FromDays(30);

    private readonly string _filePath;
    private readonly TimeSpan _freshness;
    private readonly Func<DateTimeOffset> _now;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public PersistentReputationCache(string? filePath = null, TimeSpan? freshness = null, Func<DateTimeOffset>? now = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aegis", "reputation-cache.json");
        _freshness = freshness ?? DefaultFreshness;
        _now = now ?? (static () => DateTimeOffset.UtcNow);
        Load();
    }

    /// <summary>Свежий сохранённый вердикт для хэша, либо null (нет записи или устарела — нужна новая проверка).</summary>
    public FileReputation? TryGet(string sha256)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(sha256, out var entry))
            {
                return null;
            }

            if (_now() - entry.CheckedAt > _freshness)
            {
                return null; // устарело — пусть перепроверят
            }

            return new FileReputation
            {
                Hash = sha256,
                Verdict = entry.Verdict,
                MaliciousCount = entry.MaliciousCount,
                TotalEngines = entry.TotalEngines,
            };
        }
    }

    /// <summary>Запомнить вердикт (только определённые — не «лимит»/ошибку сети) и сохранить на диск.</summary>
    public void Set(FileReputation reputation)
    {
        if (reputation.Verdict is ReputationVerdict.RateLimited)
        {
            return;
        }

        lock (_gate)
        {
            _entries[reputation.Hash] = new Entry
            {
                Verdict = reputation.Verdict,
                MaliciousCount = reputation.MaliciousCount,
                TotalEngines = reputation.TotalEngines,
                CheckedAt = _now(),
            };
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var data = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(_filePath));
            if (data is null)
            {
                return;
            }

            foreach (var (hash, entry) in data)
            {
                _entries[hash] = entry;
            }
        }
        catch (Exception)
        {
            // Битый/недоступный файл кэша — не критично, начинаем с пустого.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            // Атомарно: обрыв на записи не оставит кэш битым (пишем в .tmp, затем переименовываем поверх).
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_entries));
            File.Move(temp, _filePath, overwrite: true);
        }
        catch (Exception)
        {
            // Не смогли записать кэш — не критично (просто не сохранится между запусками).
        }
    }

    private sealed record Entry
    {
        public required ReputationVerdict Verdict { get; init; }
        public int MaliciousCount { get; init; }
        public int TotalEngines { get; init; }
        public required DateTimeOffset CheckedAt { get; init; }
    }
}
