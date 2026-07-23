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
    public string Backup(RegistryHive hive, string subKey, string valueName, string description) =>
        BackupMany([new RegistryValueRef(hive, subKey, valueName)], description);

    /// <summary>
    /// Сохранить состояние НЕСКОЛЬКИХ значений одной правки под общим идентификатором. Нужен там, где одна
    /// кнопка меняет несколько значений (брандмауэр в трёх профилях, игровые твики): откат по этому id
    /// возвращает их все, а не одно — иначе кнопка «Вернуть» врала бы о полноте отката.
    /// </summary>
    public string BackupMany(IReadOnlyList<RegistryValueRef> targets, string description)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
        {
            throw new ArgumentException("Нужно указать хотя бы одно значение реестра для бэкапа.", nameof(targets));
        }

        var states = targets.Select(ReadState).ToList();
        var first = states[0];

        var backup = new RegistryValueBackup
        {
            Id = Guid.NewGuid().ToString("N"),
            Hive = first.Hive,
            SubKey = first.SubKey,
            ValueName = first.ValueName,
            Existed = first.Existed,
            ValueKind = first.ValueKind,
            Value = first.Value,
            Additional = states.Skip(1).ToList(),
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Directory.CreateDirectory(Folder);
        AtomicFile.WriteAllText(PathFor(backup.Id), JsonSerializer.Serialize(backup));
        return backup.Id;
    }

    /// <summary>Прочитать текущее состояние значения. Бросает, если прочитать не удалось (см. комментарий внутри).</summary>
    private static RegistryValueBackupItem ReadState(RegistryValueRef target)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(target.Hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(target.SubKey);
            // Без раскрытия %ENV% — храним исходное значение как есть (важно для REG_EXPAND_SZ).
            var current = key?.GetValue(target.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (current is not null && key is not null)
            {
                var valueKind = key.GetValueKind(target.ValueName);
                return new RegistryValueBackupItem
                {
                    Hive = HiveName(target.Hive),
                    SubKey = target.SubKey,
                    ValueName = target.ValueName,
                    Existed = true,
                    ValueKind = valueKind.ToString(),
                    Value = RegistryValueCodec.Encode(current, valueKind),
                };
            }

            // current == null — значение ДЕЙСТВИТЕЛЬНО отсутствует: Existed=false корректно (откат его удалит).
            return new RegistryValueBackupItem
            {
                Hive = HiveName(target.Hive),
                SubKey = target.SubKey,
                ValueName = target.ValueName,
                Existed = false,
            };
        }
        catch (Exception ex)
        {
            // Прочитать прежнее значение НЕ удалось — это НЕ то же, что «его нет». Нельзя записать бэкап
            // «значения не было», иначе откат УДАЛИТ живое значение. Бросаем — правка отменится без изменений.
            throw new InvalidOperationException(
                "Не удалось сохранить прежнее значение реестра для отката — правка отменена ради безопасности.", ex);
        }
    }

    /// <summary>Откатить значение к сохранённому состоянию. Возвращает false, если бэкап с таким id не наш (не найден).</summary>
    public bool Restore(string id)
    {
        var backup = Load(id);
        if (backup is null)
        {
            return false;
        }

        RestoreOne(new RegistryValueBackupItem
        {
            Hive = backup.Hive,
            SubKey = backup.SubKey,
            ValueName = backup.ValueName,
            Existed = backup.Existed,
            ValueKind = backup.ValueKind,
            Value = backup.Value,
        });

        // Групповая правка: возвращаем ВСЕ значения записи, иначе откат будет частичным и незаметно неполным.
        foreach (var item in backup.Additional)
        {
            RestoreOne(item);
        }

        return true;
    }

    private static void RestoreOne(RegistryValueBackupItem item)
    {
        using var baseKey = RegistryKey.OpenBaseKey(ParseHive(item.Hive), RegistryView.Default);
        using var key = baseKey.CreateSubKey(item.SubKey, writable: true);

        if (item.Existed)
        {
            var valueKind = Enum.TryParse<RegistryValueKind>(item.ValueKind, out var parsed)
                ? parsed
                : RegistryValueKind.String;
            var data = RegistryValueCodec.Decode(item.Value, valueKind);
            key.SetValue(item.ValueName, data, valueKind);
        }
        else
        {
            key.DeleteValue(item.ValueName, throwOnMissingValue: false);
        }
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
