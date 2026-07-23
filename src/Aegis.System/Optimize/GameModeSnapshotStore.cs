using System.Text.Json;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Optimize;

/// <summary>
/// Хранит состояние системы до включения игрового режима (JSON в %LOCALAPPDATA%\Aegis). Файл переживает
/// перезапуск программы — иначе после её падения службы остались бы выключенными, а схема питания
/// переключённой, и человек об этом не узнал бы.
///
/// Прочитанный файл ПРОВЕРЯЕТСЯ: восстанавливаем только известные службы и заранее разрешённые значения
/// реестра. Файл лежит в профиле пользователя, его может подменить любая программа — без проверки подменённый
/// снимок стал бы способом чужими руками (у нас есть права администратора) записать что угодно в реестр.
/// </summary>
public sealed class GameModeSnapshotStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "game-mode-snapshot.json");

    /// <summary>Службы, которые игровой режим вправе трогать (и, значит, восстанавливать).</summary>
    public static readonly IReadOnlySet<string> AllowedServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WSearch",    // поиск Windows — индексирует диск в фоне
        "SysMain",    // SuperFetch — предзагрузка, мешает при нехватке памяти
        "wuauserv",   // центр обновления
        "Spooler",    // очередь печати
        "DiagTrack",  // телеметрия
        "BITS",       // фоновая передача файлов (докачка обновлений)
    };

    /// <summary>Значения реестра, которые игровой режим вправе менять и восстанавливать (куст|путь|имя).</summary>
    public static readonly IReadOnlySet<string> AllowedRegistryValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        @"HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR|AppCaptureEnabled",
        @"HKCU|System\GameConfigStore|GameDVR_Enabled",
        @"HKCU|System\GameConfigStore|GameDVR_FSEBehaviorMode",
        @"HKCU|System\GameConfigStore|GameDVR_HonorUserFSEBehaviorMode",
        @"HKCU|System\GameConfigStore|GameDVR_DXGIHonorFSEWindowsCompatible",
        @"HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize|EnableTransparency",
    };

    /// <summary>Префикс путей сетевых интерфейсов — их GUID заранее неизвестен, поэтому проверяем форму пути.</summary>
    private const string NetworkInterfacesPrefix = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\";

    /// <summary>Значения сетевой задержки, разрешённые в интерфейсах (только они и никакие другие).</summary>
    private static readonly IReadOnlySet<string> NetworkValueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "TcpAckFrequency", "TCPNoDelay",
    };

    /// <summary>Сохранить снимок (атомарно — иначе оборванная запись оставит нечитаемый файл).</summary>
    public void Save(GameModeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(snapshot));
    }

    /// <summary>Прочитать снимок. null — режим не включён или файл нечитаемый/недоверенный.</summary>
    public GameModeSnapshot? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var snapshot = JsonSerializer.Deserialize<GameModeSnapshot>(File.ReadAllText(FilePath));
            return snapshot is null ? null : Sanitize(snapshot);
        }
        catch (Exception)
        {
            // Битый файл — считаем, что режим не включён (лучше не восстанавливать, чем восстановить мусор).
            return null;
        }
    }

    /// <summary>Удалить снимок (режим выключен).</summary>
    public void Clear()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (Exception)
        {
            // Файла нет или он занят — не критично: при следующем включении он будет перезаписан.
        }
    }

    /// <summary>
    /// Оставляет в снимке только то, что игровой режим действительно мог менять. Всё постороннее выбрасывается:
    /// снимок с диска — не доверенный источник (см. комментарий к классу).
    /// </summary>
    internal static GameModeSnapshot Sanitize(GameModeSnapshot snapshot) => snapshot with
    {
        Services = snapshot.Services
            .Where(service => AllowedServices.Contains(service.Name) && service.StartType is >= 0 and <= 4)
            .ToList(),
        RegistryValues = snapshot.RegistryValues.Where(IsAllowedRegistryValue).ToList(),
        PowerSchemeGuid = IsGuid(snapshot.PowerSchemeGuid) ? snapshot.PowerSchemeGuid : null,
        ClosedApps = snapshot.ClosedApps.Where(name => name.Length is > 0 and <= 260).ToList(),
    };

    /// <summary>Разрешено ли игровому режиму трогать это значение реестра.</summary>
    internal static bool IsAllowedRegistryValue(GameModeRegistryState state)
    {
        if (string.IsNullOrWhiteSpace(state.Hive) || string.IsNullOrWhiteSpace(state.SubKey) || string.IsNullOrWhiteSpace(state.ValueName))
        {
            return false;
        }

        var key = $"{state.Hive}|{state.SubKey.Trim('\\')}|{state.ValueName}";
        if (AllowedRegistryValues.Contains(key))
        {
            return true;
        }

        // Сетевые интерфейсы: путь вида …\Interfaces\{GUID} и только два известных значения.
        if (!string.Equals(state.Hive, "HKLM", StringComparison.OrdinalIgnoreCase)
            || !state.SubKey.StartsWith(NetworkInterfacesPrefix, StringComparison.OrdinalIgnoreCase)
            || !NetworkValueNames.Contains(state.ValueName))
        {
            return false;
        }

        var interfaceId = state.SubKey[NetworkInterfacesPrefix.Length..].Trim('\\');
        return IsGuid(interfaceId.Trim('{', '}'));
    }

    /// <summary>Строка — это GUID (в фигурных скобках или без).</summary>
    private static bool IsGuid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value.Trim('{', '}'), out _);
}
