using System.Text.Json;
using Microsoft.Win32;
using Aegis.System.Internal;

namespace Aegis.System.Backup;

/// <summary>
/// Хранилище бэкапов значений реестра (JSON в %LOCALAPPDATA%\Aegis\backups\registry). Перед правкой
/// сохраняет прежнее значение; при откате — восстанавливает его (или удаляет, если значения не было).
/// </summary>
public sealed class RegistryBackupStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "backups", "registry");

    /// <summary>Сохранить текущее состояние значения и вернуть идентификатор бэкапа.</summary>
    public string Backup(RegistryHive hive, string subKey, string valueName, string description)
    {
        bool existed = false;
        string? kind = null;
        string? value = null;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            // Без раскрытия %ENV% — храним исходное значение как есть (важно для REG_EXPAND_SZ).
            var current = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (current is not null && key is not null)
            {
                existed = true;
                var valueKind = key.GetValueKind(valueName);
                kind = valueKind.ToString();
                value = RegistryValueCodec.Encode(current, valueKind);
            }
            // current == null — значение ДЕЙСТВИТЕЛЬНО отсутствует: existed=false корректно (откат его удалит).
        }
        catch (Exception ex)
        {
            // Прочитать прежнее значение НЕ удалось — это НЕ то же, что «его нет». Нельзя записать бэкап
            // «значения не было», иначе откат УДАЛИТ живое значение. Бросаем — правка отменится без изменений.
            throw new InvalidOperationException(
                "Не удалось сохранить прежнее значение реестра для отката — правка отменена ради безопасности.", ex);
        }

        var backup = new RegistryValueBackup
        {
            Id = Guid.NewGuid().ToString("N"),
            Hive = HiveName(hive),
            SubKey = subKey,
            ValueName = valueName,
            Existed = existed,
            ValueKind = kind,
            Value = value,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Directory.CreateDirectory(Folder);
        File.WriteAllText(PathFor(backup.Id), JsonSerializer.Serialize(backup));
        return backup.Id;
    }

    /// <summary>Откатить значение к сохранённому состоянию. Возвращает false, если бэкап с таким id не наш (не найден).</summary>
    public bool Restore(string id)
    {
        var backup = Load(id);
        if (backup is null)
        {
            return false;
        }

        using var baseKey = RegistryKey.OpenBaseKey(ParseHive(backup.Hive), RegistryView.Default);
        using var key = baseKey.CreateSubKey(backup.SubKey, writable: true);

        if (backup.Existed)
        {
            var valueKind = Enum.TryParse<RegistryValueKind>(backup.ValueKind, out var parsed)
                ? parsed
                : RegistryValueKind.String;
            var data = RegistryValueCodec.Decode(backup.Value, valueKind);
            key.SetValue(backup.ValueName, data, valueKind);
        }
        else
        {
            key.DeleteValue(backup.ValueName, throwOnMissingValue: false);
        }

        return true;
    }

    /// <summary>Все сохранённые бэкапы (новые сверху).</summary>
    public IReadOnlyList<RegistryValueBackup> List()
    {
        var result = new List<RegistryValueBackup>();
        if (!Directory.Exists(Folder))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                var backup = JsonSerializer.Deserialize<RegistryValueBackup>(File.ReadAllText(file));
                if (backup is not null)
                {
                    result.Add(backup);
                }
            }
            catch (Exception)
            {
                // Битый файл бэкапа — пропускаем.
            }
        }

        return result.OrderByDescending(b => b.CreatedAt).ToList();
    }

    private static RegistryValueBackup? Load(string id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RegistryValueBackup>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string PathFor(string id) => Path.Combine(Folder, id + ".json");

    private static string HiveName(RegistryHive hive) => RegistryHiveNames.ToShortName(hive);

    private static RegistryHive ParseHive(string name) => RegistryHiveNames.ToHive(name);
}
