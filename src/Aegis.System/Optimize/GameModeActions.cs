using System.Diagnostics;
using Microsoft.Win32;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.System.Internal;

namespace Aegis.System.Optimize;

/// <summary>
/// Конкретные системные действия игрового режима: службы, программы, питание, реестр, сетевая задержка.
/// Каждое действие возвращает прежнее состояние, чтобы его можно было вернуть. Всё best-effort: если что-то
/// не получилось (нет прав, службы нет в системе) — говорим об этом, но не роняем весь режим.
/// </summary>
internal sealed class GameModeActions
{
    /// <summary>Высокая производительность — стандартная схема Windows с постоянным GUID.</summary>
    private const string HighPerformanceScheme = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    private const string GameDvrKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string GameConfigKey = @"System\GameConfigStore";
    private const string PersonalizeKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string NetworkInterfacesKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    /// <summary>Службы, которые приостанавливаем на время игры (в порядке полезности).</summary>
    private static readonly string[] PausedServices = ["WSearch", "SysMain", "wuauserv", "Spooler", "DiagTrack", "BITS"];

    // ─── Службы ───────────────────────────────────────────────────────────────

    /// <summary>Остановить фоновые службы, вернув их прежнее состояние для отката.</summary>
    public async Task<IReadOnlyList<GameModeServiceState>> PauseServicesAsync(CancellationToken cancellationToken)
    {
        var stopped = new List<GameModeServiceState>();

        foreach (var name in PausedServices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startType = ReadServiceStartType(name);
            if (startType is null)
            {
                continue; // службы нет в этой сборке Windows
            }

            var wasRunning = await IsServiceRunningAsync(name, cancellationToken).ConfigureAwait(false);
            if (!wasRunning && startType == 4)
            {
                continue; // уже отключена — трогать нечего
            }

            // Останавливаем, но НЕ отключаем запуск: после игры служба должна вернуться сама даже при
            // аварийном завершении программы (снимок — это подстраховка, а не единственный путь назад).
            await ProcessRunner.RunAsync(ProcessRunner.System("sc.exe"), $"stop \"{name}\"", cancellationToken)
                .ConfigureAwait(false);

            stopped.Add(new GameModeServiceState { Name = name, StartType = startType.Value, WasRunning = wasRunning });
        }

        return stopped;
    }

    /// <summary>Вернуть службы в прежнее состояние (запустить те, что работали).</summary>
    public async Task<int> RestoreServicesAsync(IReadOnlyList<GameModeServiceState> services, CancellationToken cancellationToken)
    {
        var restored = 0;
        foreach (var service in services)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!service.WasRunning)
            {
                continue;
            }

            var code = await ProcessRunner
                .RunAsync(ProcessRunner.System("sc.exe"), $"start \"{service.Name}\"", cancellationToken)
                .ConfigureAwait(false);

            // Код 1056 = «служба уже запущена» — тоже успех (Windows могла поднять её сама).
            if (code is 0 or 1056)
            {
                restored++;
            }
        }

        return restored;
    }

    private static int? ReadServiceStartType(string name) =>
        RegistryReader.GetDword(RegistryHive.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{name}", "Start");

    private static async Task<bool> IsServiceRunningAsync(string name, CancellationToken cancellationToken)
    {
        var output = await ProcessRunner
            .RunForOutputAsync(ProcessRunner.System("sc.exe"), $"query \"{name}\"", cancellationToken)
            .ConfigureAwait(false);
        return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    // ─── Фоновые программы ────────────────────────────────────────────────────

    /// <summary>
    /// Закрыть фоновые программы (браузеры, мессенджеры, обновляторы). Сначала мягко — просьбой закрыться,
    /// чтобы не потерять несохранённое; если программа не реагирует, оставляем её в покое.
    /// </summary>
    public IReadOnlyList<string> CloseBackgroundApps(CancellationToken cancellationToken)
    {
        var closed = new List<string>();

        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = process.ProcessName + ".exe";
                if (!GameProcessCatalog.IsClosable(name) || process.Id <= 4)
                {
                    continue;
                }

                // Мягкое закрытие: окно получает обычный запрос на выход, как при нажатии на крестик.
                if (process.CloseMainWindow() && process.WaitForExit(3000))
                {
                    closed.Add(GameProcessCatalog.DisplayName(name));
                    continue;
                }

                // Обновляторы и фоновые помощники окон не имеют — их закрываем принудительно: несохранённых
                // данных у них нет. Пользовательские программы (браузер, мессенджер) не трогаем.
                if (IsSilentHelper(name))
                {
                    process.Kill();
                    if (process.WaitForExit(3000))
                    {
                        closed.Add(GameProcessCatalog.DisplayName(name));
                    }
                }
            }
            catch (Exception)
            {
                // Процесс исчез или нет доступа — пропускаем (best-effort).
            }
            finally
            {
                process.Dispose();
            }
        }

        return closed;
    }

    /// <summary>Фоновый помощник без окон — такое можно закрывать принудительно, данные не теряются.</summary>
    private static bool IsSilentHelper(string processName) =>
        GameProcessCatalog.BackgroundApps.Any(app =>
            string.Equals(app.ProcessName, processName, StringComparison.OrdinalIgnoreCase)
            && app.Kind is BackgroundAppKind.Updater or BackgroundAppKind.Sync);

    // ─── Электропитание ───────────────────────────────────────────────────────

    /// <summary>Переключить схему питания на «Высокую производительность». Возвращает прежний GUID.</summary>
    public async Task<string?> SetHighPerformanceAsync(CancellationToken cancellationToken)
    {
        var current = await ReadActiveSchemeAsync(cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return null;
        }

        var code = await ProcessRunner
            .RunAsync(ProcessRunner.System("powercfg.exe"), $"/setactive {HighPerformanceScheme}", cancellationToken)
            .ConfigureAwait(false);

        return code == 0 ? current : null;
    }

    /// <summary>Вернуть прежнюю схему питания.</summary>
    public async Task<bool> RestorePowerSchemeAsync(string guid, CancellationToken cancellationToken)
    {
        var code = await ProcessRunner
            .RunAsync(ProcessRunner.System("powercfg.exe"), $"/setactive {guid}", cancellationToken)
            .ConfigureAwait(false);
        return code == 0;
    }

    private static async Task<string?> ReadActiveSchemeAsync(CancellationToken cancellationToken)
    {
        var output = await ProcessRunner
            .RunForOutputAsync(ProcessRunner.System("powercfg.exe"), "/getactivescheme", cancellationToken)
            .ConfigureAwait(false);

        // Формат: «Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Сбалансированная)».
        foreach (var token in output.Split([' ', ':', '(', ')', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token, out var guid))
            {
                return guid.ToString();
            }
        }

        return null;
    }

    // ─── Реестр: игровая панель, прозрачность ─────────────────────────────────

    /// <summary>Отключить Game Bar и запись игрового видео. Возвращает прежние значения для отката.</summary>
    public IReadOnlyList<GameModeRegistryState> DisableGameBar() =>
    [
        SetUserDword(GameDvrKey, "AppCaptureEnabled", 0),
        SetUserDword(GameConfigKey, "GameDVR_Enabled", 0),
        SetUserDword(GameConfigKey, "GameDVR_FSEBehaviorMode", 2),
        SetUserDword(GameConfigKey, "GameDVR_HonorUserFSEBehaviorMode", 0),
        SetUserDword(GameConfigKey, "GameDVR_DXGIHonorFSEWindowsCompatible", 0),
    ];

    /// <summary>Отключить прозрачность окон (немного разгружает видеокарту).</summary>
    public GameModeRegistryState DisableTransparency() => SetUserDword(PersonalizeKey, "EnableTransparency", 0);

    /// <summary>
    /// Убрать сетевую задержку (алгоритм Нейгла) на всех сетевых интерфейсах: пакеты уходят сразу, а не
    /// копятся ради экономии. Для сетевых игр это меньше задержка; на обычную работу влияния практически нет.
    /// </summary>
    public IReadOnlyList<GameModeRegistryState> ReduceNetworkLatency()
    {
        var changed = new List<GameModeRegistryState>();

        try
        {
            using var interfaces = Registry.LocalMachine.OpenSubKey(NetworkInterfacesKey);
            foreach (var name in interfaces?.GetSubKeyNames() ?? [])
            {
                // Только настоящие интерфейсы (их имена — GUID в фигурных скобках).
                if (!Guid.TryParse(name.Trim('{', '}'), out _))
                {
                    continue;
                }

                var subKey = $@"{NetworkInterfacesKey}\{name}";
                changed.Add(SetMachineDword(subKey, "TcpAckFrequency", 1));
                changed.Add(SetMachineDword(subKey, "TCPNoDelay", 1));
            }
        }
        catch (Exception)
        {
            // Нет доступа к веткам интерфейсов — сетевую задержку просто не трогаем.
        }

        return changed;
    }

    /// <summary>Вернуть значения реестра к состоянию из снимка. Возвращает, сколько значений восстановлено.</summary>
    public int RestoreRegistryValues(IReadOnlyList<GameModeRegistryState> values)
    {
        var restored = 0;

        foreach (var state in values)
        {
            // Повторная проверка перед записью: снимок мог быть подменён на диске (защита в глубину).
            if (!GameModeSnapshotStore.IsAllowedRegistryValue(state))
            {
                continue;
            }

            try
            {
                var root = string.Equals(state.Hive, "HKCU", StringComparison.OrdinalIgnoreCase)
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

                using var key = root.CreateSubKey(state.SubKey, writable: true);
                if (state.Value is int value)
                {
                    key.SetValue(state.ValueName, value, RegistryValueKind.DWord);
                }
                else
                {
                    key.DeleteValue(state.ValueName, throwOnMissingValue: false);
                }

                restored++;
            }
            catch (Exception)
            {
                // Ключ удалён/нет прав — остальные значения всё равно вернём.
            }
        }

        return restored;
    }

    private static GameModeRegistryState SetUserDword(string subKey, string valueName, int value) =>
        SetDword(Registry.CurrentUser, "HKCU", subKey, valueName, value);

    private static GameModeRegistryState SetMachineDword(string subKey, string valueName, int value) =>
        SetDword(Registry.LocalMachine, "HKLM", subKey, valueName, value);

    /// <summary>Записывает значение и возвращает то, что было до этого (для отката).</summary>
    private static GameModeRegistryState SetDword(RegistryKey root, string hiveName, string subKey, string valueName, int value)
    {
        int? previous = null;
        try
        {
            using var read = root.OpenSubKey(subKey);
            previous = read?.GetValue(valueName) as int?;

            using var write = root.CreateSubKey(subKey, writable: true);
            write.SetValue(valueName, value, RegistryValueKind.DWord);
        }
        catch (Exception)
        {
            // Нет прав на ветку — значение не изменится, откат для него окажется пустым действием.
        }

        return new GameModeRegistryState
        {
            Hive = hiveName,
            SubKey = subKey,
            ValueName = valueName,
            Value = previous,
        };
    }

    // ─── Запрет засыпания ─────────────────────────────────────────────────────

    /// <summary>Не давать компьютеру гасить экран и засыпать, пока идёт игра.</summary>
    public static void KeepAwake(bool enabled) => SleepBlocker.Set(enabled);
}
