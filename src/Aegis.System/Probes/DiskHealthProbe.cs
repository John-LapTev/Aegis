using System.Globalization;
using System.Management;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Здоровье и заполнение дисков через WMI (root\Microsoft\Windows\Storage). Только читает.
///
/// Главная сущность — <c>MSFT_Disk</c>: в ней есть и НОМЕР диска, и статус здоровья. От номера НАПРЯМУЮ
/// считаем заполнение (Number → MSFT_Partition → MSFT_Volume) — без сопоставления разных таблиц, поэтому
/// работает и для внешних USB-дисков. Износ/температуру добавляем best-effort из MSFT_PhysicalDisk +
/// счётчика надёжности (если совпадёт по серийнику/имени) — это вторично и на заполнение не влияет.
/// </summary>
public sealed class DiskHealthProbe : IDiskHealthProbe
{
    public Task<IReadOnlyList<SmartDriveHealth>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var byNumber = new SortedDictionary<int, SmartDriveHealth>(); // по номеру диска — стабильный порядок (0,1,…)

        try
        {
            var (fillByNumber, partitionedDisks, letterByNumber) = ReadFillByDiskNumber();
            var reliability = ReadReliabilityByDisk(); // best-effort износ/температура (серийник/имя → значения)

            using var disks = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT Number, FriendlyName, HealthStatus, SerialNumber FROM MSFT_Disk");

            foreach (var item in disks.Get())
            {
                using var disk = (ManagementObject)item;
                var number = ToInt(disk["Number"]);
                if (number < 0)
                {
                    continue;
                }

                var name = disk["FriendlyName"]?.ToString();
                var serial = disk["SerialNumber"]?.ToString();
                var (wear, temperature) = LookupReliability(reliability, serial, name);

                byNumber[number] = new SmartDriveHealth
                {
                    Name = string.IsNullOrWhiteSpace(name) ? "Диск" : name,
                    Level = ToLevel(ToInt(disk["HealthStatus"])),
                    Model = name,
                    PercentLifeUsed = wear,
                    TemperatureCelsius = temperature,
                    FillPercent = fillByNumber.TryGetValue(number, out var fill) ? fill : null,
                    // Раздел есть, а заполнение не посчиталось → Windows не читает формат (RAW). Так у внешнего ADATA.
                    FilesystemUnreadable = partitionedDisks.Contains(number) && !fillByNumber.ContainsKey(number),
                    Letter = letterByNumber.TryGetValue(number, out var driveLetter) ? driveLetter : null,
                };
            }
        }
        catch (Exception)
        {
            // WMI недоступен (не Windows) или нет данных — пусто.
        }

        return Task.FromResult<IReadOnlyList<SmartDriveHealth>>(byNumber.Values.ToList());
    }

    /// <summary>
    /// Заполнение каждого диска (% занято), ключ — НОМЕР диска (тот же, что у MSFT_Disk и в «Управлении дисками»).
    /// Раздел связываем с томом по GUID-пути тома (<c>\\?\Volume{...}\</c> из MSFT_Partition.AccessPaths ↔ MSFT_Volume.Path),
    /// а НЕ по букве — у внешних USB-дисков буква висит только на томе, на разделе её нет, и связь по букве терялась.
    /// </summary>
    private static (Dictionary<int, int> Fill, HashSet<int> Partitioned, Dictionary<int, char> Letters) ReadFillByDiskNumber()
    {
        var result = new Dictionary<int, int>();
        var partitioned = new HashSet<int>(); // диски, у которых ЕСТЬ раздел (для отличия RAW-диска от пустого)
        var letterByDisk = new Dictionary<int, char>();

        try
        {
            // Том по его пути \\?\Volume{...}\ → (размер, свободно) + буква тома (если есть).
            var byPath = new Dictionary<string, (long Size, long Free)>(StringComparer.OrdinalIgnoreCase);
            var letterByPath = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
            using (var volumes = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT Path, Size, SizeRemaining, DriveLetter FROM MSFT_Volume"))
            {
                foreach (var v in volumes.Get())
                {
                    using var volume = (ManagementObject)v;
                    var path = volume["Path"]?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        byPath[path] = (ToLong(volume["Size"]), ToLong(volume["SizeRemaining"]));
                        var letter = ToLetter(volume["DriveLetter"]);
                        if (letter != '\0')
                        {
                            letterByPath[path] = letter;
                        }
                    }
                }
            }

            var perDisk = new Dictionary<int, (long Size, long Free)>();
            using (var partitions = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DiskNumber, AccessPaths FROM MSFT_Partition"))
            {
                foreach (var p in partitions.Get())
                {
                    using var partition = (ManagementObject)p;
                    var diskNumber = ToInt(partition["DiskNumber"]);
                    if (diskNumber < 0)
                    {
                        continue;
                    }

                    partitioned.Add(diskNumber); // у диска есть раздел (даже если том не читается — RAW)
                    if (partition["AccessPaths"] is not string[] accessPaths)
                    {
                        continue;
                    }

                    foreach (var accessPath in accessPaths)
                    {
                        if (accessPath is not null && byPath.TryGetValue(accessPath, out var space))
                        {
                            var current = perDisk.TryGetValue(diskNumber, out var acc) ? acc : (Size: 0L, Free: 0L);
                            perDisk[diskNumber] = (current.Size + space.Size, current.Free + space.Free);
                            // Буква диска — первая встреченная (например, C); для подписи на иконке.
                            if (letterByPath.TryGetValue(accessPath, out var diskLetter) && !letterByDisk.ContainsKey(diskNumber))
                            {
                                letterByDisk[diskNumber] = diskLetter;
                            }

                            break; // том этого раздела найден — дальше пути не нужны
                        }
                    }
                }
            }

            foreach (var pair in perDisk)
            {
                if (pair.Value.Size > 0)
                {
                    var fill = (int)Math.Round((1 - ((double)pair.Value.Free / pair.Value.Size)) * 100);
                    result[pair.Key] = Math.Clamp(fill, 0, 100);
                }
            }
        }
        catch (Exception)
        {
            // WMI недоступен/нет данных — диски останутся без процентов заполнения.
        }

        return (result, partitioned, letterByDisk);
    }

    /// <summary>Износ/температура каждого физического диска (best-effort) — для последующего совпадения по серийнику/имени.</summary>
    private static List<(string? Serial, string? Name, int? Wear, int? Temperature)> ReadReliabilityByDisk()
    {
        var list = new List<(string?, string?, int?, int?)>();

        try
        {
            // Износ/температуру всех дисков берём ОДНИМ запросом (по DeviceId), а не по диску в цикле.
            var counters = ReadAllReliabilityCounters();

            using var physical = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DeviceId, SerialNumber, FriendlyName FROM MSFT_PhysicalDisk");
            foreach (var item in physical.Get())
            {
                using var disk = (ManagementObject)item;
                var deviceId = disk["DeviceId"]?.ToString();
                var (wear, temperature) = deviceId is not null && counters.TryGetValue(deviceId, out var c)
                    ? c
                    : (null, null);
                list.Add((
                    disk["SerialNumber"]?.ToString()?.Trim(),
                    disk["FriendlyName"]?.ToString()?.Trim(),
                    wear,
                    temperature));
            }
        }
        catch (Exception)
        {
            // Не критично — диск просто без износа/температуры.
        }

        return list;
    }

    private static (int? Wear, int? Temperature) LookupReliability(
        List<(string? Serial, string? Name, int? Wear, int? Temperature)> list, string? serial, string? name)
    {
        var serialKey = serial?.Trim();
        if (!string.IsNullOrEmpty(serialKey))
        {
            foreach (var entry in list)
            {
                if (string.Equals(entry.Serial, serialKey, StringComparison.OrdinalIgnoreCase))
                {
                    return (entry.Wear, entry.Temperature);
                }
            }
        }

        var nameKey = name?.Trim();
        if (!string.IsNullOrEmpty(nameKey))
        {
            foreach (var entry in list)
            {
                if (entry.Name is { Length: > 0 } entryName &&
                    (string.Equals(entryName, nameKey, StringComparison.OrdinalIgnoreCase) ||
                     entryName.Contains(nameKey, StringComparison.OrdinalIgnoreCase) ||
                     nameKey.Contains(entryName, StringComparison.OrdinalIgnoreCase)))
                {
                    return (entry.Wear, entry.Temperature);
                }
            }
        }

        return (null, null);
    }

    /// <summary>Износ/температура ВСЕХ дисков одним запросом → словарь по DeviceId (без запроса на каждый диск).</summary>
    private static Dictionary<string, (int? Wear, int? Temperature)> ReadAllReliabilityCounters()
    {
        var result = new Dictionary<string, (int? Wear, int? Temperature)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DeviceId, Wear, Temperature FROM MSFT_StorageReliabilityCounter");

            foreach (var item in searcher.Get())
            {
                using var counter = (ManagementObject)item;
                var deviceId = counter["DeviceId"]?.ToString();
                if (string.IsNullOrEmpty(deviceId))
                {
                    continue;
                }

                var wear = ToInt(counter["Wear"]);
                var temperature = ToInt(counter["Temperature"]);
                result[deviceId] = (wear >= 0 ? wear : null, temperature > 0 ? temperature : null);
            }
        }
        catch (Exception)
        {
            // Счётчик надёжности недоступен — не критично, диски просто без износа/температуры.
        }

        return result;
    }

    /// <summary>Буква диска из MSFT_Volume/MSFT_Partition (WMI char16 приходит как char/ushort/строка) → верхний регистр; '\0' если нет.</summary>
    private static char ToLetter(object? value)
    {
        var letter = value switch
        {
            char c => c,
            ushort u => (char)u,
            short s => (char)s,
            int i => (char)i,
            string str when str.Length > 0 => str[0],
            _ => '\0',
        };

        letter = char.ToUpperInvariant(letter);
        return char.IsLetter(letter) ? letter : '\0';
    }

    private static long ToLong(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static int ToInt(object? value)
    {
        try
        {
            return value is null ? -1 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static SmartHealthLevel ToLevel(int healthStatus) => healthStatus switch
    {
        1 => SmartHealthLevel.Warning,   // Warning
        2 => SmartHealthLevel.Critical,  // Unhealthy
        // 0 Healthy и -1 «нет данных» — не пугаем пользователя.
        _ => SmartHealthLevel.Good,
    };
}
