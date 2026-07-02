using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aegis.App.Services;
using Aegis.App.ViewModels;
using Aegis.App.Views;
using Aegis.Core.Abstractions;
using Aegis.Core.Fixing;
using Aegis.Core.Scanning;
using Aegis.System.Backup;
using Aegis.System.Devices;
using Aegis.System.Fixing;
using Aegis.System.Reputation;
using Aegis.Threats.Ai;
using Aegis.Threats.VirusTotal;
using Aegis.Threats.Web;
using Aegis.Scanners.Apps;
using Aegis.Scanners.Audio;
using Aegis.Scanners.Autostart;
using Aegis.Scanners.Drivers;
using Aegis.Scanners.Files;
using Aegis.Scanners.Maintenance;
using Aegis.Scanners.Online;
using Aegis.Scanners.Privacy;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Programs;
using Aegis.Scanners.Processes;
using Aegis.Scanners.Registry;
using Aegis.Scanners.Stress;
using Aegis.Scanners.Settings;
using Aegis.Scanners.SystemInfo;
using Aegis.Scanners.Threats;
using Aegis.Scanners.Utilities;
using Aegis.Scanners.Junk;
using Aegis.System.Probes;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Aegis.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(LogFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();

        _services = ConfigureServices();
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Запуск после перезагрузки (RunOnce, флаг --confirm-rollback) для проверки рискованных правок:
            // показываем окно «всё работает?» вместо главного. Не подтвердят → откат по точке восстановления.
            var scheduler = _services.GetRequiredService<IRebootRollbackScheduler>();
            if ((desktop.Args ?? []).Contains("--confirm-rollback") && scheduler.GetPending() is { } pending)
            {
                var restore = _services.GetRequiredService<IRestorePointService>();
                Log.Information("Проверка отката после перезагрузки: {Desc}", pending.Description);
                desktop.MainWindow = new RollbackConfirmWindow(
                    pending.Description,
                    async () =>
                    {
                        // Откатываем каждую правку пакета по её бэкапу (реестр/задачи/карантин) — best-effort:
                        // одна неудача не должна срывать остальные. Итог фиксируем в лог.
                        foreach (var backupId in pending.BackupIds)
                        {
                            try
                            {
                                await restore.RestoreAsync(backupId).ConfigureAwait(true);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Откат правки {BackupId} не удался", backupId);
                            }
                        }

                        scheduler.Clear();
                    },
                    scheduler.Clear);

                base.OnFrameworkInitializationCompleted();
                return;
            }

            var elevation = _services.GetRequiredService<IElevationService>();
            Log.Information("Запуск Aegis. Права администратора: {IsAdmin}", elevation.IsAdministrator);

            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // Без авто-скана: пользователь сам выбирает «Проверить всё» или конкретный раздел.
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "logs", "aegis-.log");

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Пробники (реальное чтение Windows).
        services.AddSingleton<IJunkProbe, JunkProbe>();
        services.AddSingleton<IAutostartProbe, AutostartProbe>();
        services.AddSingleton<IProcessProbe, ProcessProbe>();
        services.AddSingleton<ISettingsProbe, SettingsProbe>();
        services.AddSingleton<ISystemHealthProbe, SystemHealthProbe>();
        services.AddSingleton<IRegistryProbe, RegistryProbe>();
        services.AddSingleton<IPrivacyProbe, PrivacyProbe>();
        services.AddSingleton<IDiskHealthProbe, DiskHealthProbe>();
        services.AddSingleton<IMaintenanceHistoryProbe, MaintenanceHistoryProbe>();
        services.AddSingleton<INetworkThreatProbe, NetworkThreatProbe>();
        services.AddSingleton<IDangerousDriverProbe, DangerousDriverProbe>();
        services.AddSingleton<IWmiPersistenceProbe, WmiPersistenceProbe>();
        services.AddSingleton<ISuspiciousServiceProbe, SuspiciousServiceProbe>();
        services.AddSingleton<ISuspiciousTaskProbe, SuspiciousTaskProbe>();
        services.AddSingleton<IFileInventoryProbe, FileInventoryProbe>();
        services.AddSingleton<IDriverProbe, DriverProbe>();
        services.AddSingleton<INvidiaDriverCheck>(sp => new NvidiaDriverCheck(sp.GetRequiredService<HttpClient>()));
        // Именованные поисковые провайдеры (Tavily/Serper) — для раздела «Нейросети»: показать и проверять каждый.
        services.AddSingleton<IReadOnlyList<NamedSearchProvider>>(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            var named = new List<NamedSearchProvider>();
            if (LoadAiKey("TAVILY", "TavilyKey", "tavily.key") is { } tavilyKey)
            {
                named.Add(new NamedSearchProvider("Tavily", new TavilySearch(http, tavilyKey)));
            }

            if (LoadAiKey("SERPER", "SerperKey", "serper.key") is { } serperKey)
            {
                named.Add(new NamedSearchProvider("Serper", new SerperSearch(http, serperKey)));
            }

            return named;
        });

        // Веб-поиск — для веб-ответов ИИ и для поиска драйверов/утилит. Цепочка по приоритету: Tavily → Serper →
        // (Brave → Google, если заданы) → DuckDuckGo (всегда последний, бесплатный). Следующий — запас предыдущего.
        services.AddSingleton<IWebSearch>(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            var chain = sp.GetRequiredService<IReadOnlyList<NamedSearchProvider>>().Select(n => n.Search).ToList();

            if (LoadAiKey("BRAVE", "BraveKey", "brave.key") is { } braveKey)
            {
                chain.Add(new BraveSearch(http, braveKey));
            }

            if (LoadAiKey("GOOGLE_SEARCH", "GoogleKey", "google.key") is { } googleKey
                && LoadAiKey("GOOGLE_CX", "GoogleCx", "google.cx") is { } googleCx)
            {
                chain.Add(new GoogleSearch(http, googleKey, googleCx));
            }

            // Сворачиваем справа налево в вложенный FallbackWebSearch: A → (B → (… → DuckDuckGo)).
            IWebSearch search = new DuckDuckGoSearch(http);
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                search = new FallbackWebSearch(chain[i], search);
            }

            return search;
        });
        services.AddSingleton<IDeviceUpdateLookup>(sp => new DeviceUpdateLookup(sp.GetRequiredService<IWebSearch>()));
        // Датчики железа: LibreHardwareMonitor (достоверные температуры ядер/обороты/частоты). Температурный
        // пробник — LHM с откатом на стандартный ACPI/nvidia-smi (TemperatureProbe) по компонентам.
        services.AddSingleton<IHardwareSensorReader, LhmSensorReader>();
        services.AddSingleton<TemperatureProbe>();
        services.AddSingleton<ITemperatureProbe>(sp => new LhmTemperatureProbe(
            sp.GetRequiredService<IHardwareSensorReader>(), sp.GetRequiredService<TemperatureProbe>()));
        services.AddSingleton<IDiskUsageProbe, DiskUsageProbe>();
        services.AddSingleton<IAppxProbe, AppxProbe>();
        services.AddSingleton<IAudioProbe, AudioProbe>();
        services.AddSingleton<IUtilitiesProbe, UtilitiesProbe>();
        services.AddSingleton<ILeftoverProbe, LeftoverProbe>();
        services.AddSingleton<ISteamLeftoverProbe, SteamLeftoverProbe>();
        services.AddSingleton<IStaleFileProbe, StaleFileProbe>();
        services.AddSingleton<IAppCacheProbe, AppCacheProbe>();
        services.AddSingleton<IBatteryProbe, BatteryProbe>();
        services.AddSingleton<ISystemVitalsProbe, SystemVitalsProbe>();
        services.AddSingleton<IDeviceErrorProbe, DeviceErrorProbe>();
        services.AddSingleton<ICrashHistoryProbe, CrashHistoryProbe>();

        // Проверка под нагрузкой (стресс-тест): нагрузка процессора + движок на температурном пробнике.
        services.AddSingleton<ICpuLoad, CpuLoad>();
        services.AddSingleton<IStressTestEngine>(sp =>
            new StressTestEngine(sp.GetRequiredService<ICpuLoad>(), sp.GetRequiredService<ITemperatureProbe>()));

        // Сканеры (на пробниках).
        services.AddSingleton<IScanner, SystemScanner>();
        services.AddSingleton<IScanner, SystemMaintenanceScanner>();
        services.AddSingleton<IScanner, DiskHealthScanner>();
        services.AddSingleton<IScanner, TemperatureScanner>();
        services.AddSingleton<IScanner, SystemVitalsScanner>();
        services.AddSingleton<IScanner, DeviceErrorScanner>();
        services.AddSingleton<IScanner, CrashHistoryScanner>();
        services.AddSingleton<IScanner, JunkScanner>();
        services.AddSingleton<IScanner, WindowsUpdateCleanupScanner>();
        services.AddSingleton<IScanner, LargeDuplicateScanner>();
        services.AddSingleton<IScanner, DiskUsageScanner>();
        services.AddSingleton<IScanner, ProgramLeftoverScanner>();
        services.AddSingleton<IScanner, SteamLeftoverScanner>();
        services.AddSingleton<IScanner, StaleFileScanner>();
        services.AddSingleton<IScanner, AppCacheScanner>();
        services.AddSingleton<IScanner, BatteryScanner>();
        services.AddSingleton<IScanner, AutostartScanner>();
        services.AddSingleton<IScanner, ProcessesScanner>();
        services.AddSingleton<IScanner, RegistryScanner>();
        services.AddSingleton<IScanner, SettingsScanner>();
        services.AddSingleton<IScanner, PrivacyDebloatScanner>();
        services.AddSingleton<IScanner, AppxBloatScanner>();
        services.AddSingleton<IScanner, NetworkThreatScanner>();
        services.AddSingleton<IScanner, DangerousDriverScanner>();
        services.AddSingleton<IScanner, WmiPersistenceScanner>();
        services.AddSingleton<IScanner, SuspiciousServiceScanner>();
        services.AddSingleton<IScanner, SuspiciousTaskScanner>();
        services.AddSingleton<IScanner, DriversScanner>();
        services.AddSingleton<IScanner, AudioScanner>();
        services.AddSingleton<IScanner, UtilitiesScanner>();

        // Движок сканов.
        services.AddSingleton<IScanOrchestrator, ScanOrchestrator>();

        // Обратимая починка: бэкап реестра + точка восстановления + оркестратор правок + фабрика.
        services.AddSingleton<RegistryBackupStore>();
        services.AddSingleton<QuarantineStore>();
        services.AddSingleton<RegistryKeyBackupStore>();
        services.AddSingleton<ScheduledTaskBackupStore>();
        services.AddSingleton<AppxRemovalBackupStore>();
        services.AddSingleton<IRestorePointService, RestorePointService>();
        services.AddSingleton<IDeviceDriverAction, DeviceDriverAction>();
        services.AddSingleton<IRebootRollbackScheduler, RebootRollbackScheduler>();
        services.AddSingleton<IFixOrchestrator, FixOrchestrator>();
        services.AddSingleton<IFixFactory, FixFactory>();
        services.AddSingleton<IWhitelist, WhitelistStore>();

        // Общий HttpClient для внешних сервисов (VirusTotal, Gemini).
        services.AddSingleton(_ => new HttpClient());

        // Онлайн-проверка файлов: Защитник Windows (всегда) + VirusTotal (если задан ключ).
        var virusTotalKey = LoadVirusTotalKey();
        if (virusTotalKey is not null)
        {
            // Персистентный кэш вердиктов по хэшу: тот же файл не перепроверяется каждый скан и после
            // перезапуска; изменённый файл (другой хэш) проверяется заново (ловит подмену по знакомому пути).
            services.AddSingleton<IThreatReputationService>(sp =>
                new ThrottledCachingReputationService(
                    new VirusTotalClient(sp.GetRequiredService<HttpClient>(), virusTotalKey),
                    persistent: new PersistentReputationCache()));
            services.AddSingleton<IFileReputationCheck>(sp =>
                new FileReputationCheck(sp.GetRequiredService<IThreatReputationService>()));
        }
        else
        {
            services.AddSingleton<IFileReputationCheck>(_ => new FileReputationCheck(null));
        }

        // ИИ-помощник: цепочка моделей с авто-переключением по лимитам (Gemini → ChatGPT → Claude). Подключаются
        // только те, для которых задан ключ. Кончился лимит у одной — спрашиваем следующую.
        services.AddSingleton<IAiAssistant>(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            // Порядок цепочки (правки 924/935): Gemini ВСЕГДА первая; затем бесплатный ChatGPT; ПЛАТНАЯ (apiglue) —
            // в самом конце, включается ТОЛЬКО когда обе бесплатные не ответили. Так платный баланс почти не тратится.
            var providers = new List<IAiAssistant>();

            // 1) Gemini (бесплатная, умная; всегда первая по просьбе Ивана).
            if (LoadGeminiKey() is { } geminiKey)
            {
                providers.Add(new GeminiClient(http, geminiKey));
            }

            // 2) Бесплатный ChatGPT (OpenRouter, открытая модель OpenAI gpt-oss). Пул иногда занят (429) → берём следующую.
            if (LoadAiKey("OPENROUTER", "OpenRouterKey", "openrouter.key") is { } openRouterKey)
            {
                providers.Add(new OpenAiCompatibleClient(
                    http, "ChatGPT", "https://openrouter.ai/api/v1/chat/completions", "openai/gpt-oss-120b:free", openRouterKey));
            }

            // Groq и Mistral убраны по просьбе Ивана (правка 927) — слабые ответы, бесполезны.

            // 3) ПЛАТНАЯ, ПОСЛЕДНЯЯ — Claude Sonnet через apiglue (рос. GPT/Claude-прокси, оплата рос. картой).
            //    Тот же OpenAI-совместимый эндпоинт, что и раньше — сменили только модель (по просьбе Ивана). Только когда бесплатные не ответили.
            if (LoadAiKey("APIGLUE", "ApiglueKey", "apiglue.key") is { } apiglueKey)
            {
                providers.Add(new OpenAiCompatibleClient(
                    http, "Claude", "https://api.apiglue.ru/v1/chat/completions", "claude-sonnet-4-5-20250929", apiglueKey));
            }

            if (providers.Count == 0)
            {
                return new NullAiAssistant();
            }

            // Оборачиваем цепочку реальным веб-поиском (DuckDuckGo, без ключа): перед ответом ищем в интернете
            // по теме вопроса и отдаём свежие результаты со ссылками — так «в сеть лезут» ВСЕ модели одинаково.
            return new WebAugmentedAiAssistant(new FallbackAiAssistant(providers), sp.GetRequiredService<IWebSearch>());
        });

        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    private static string? LoadVirusTotalKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable("AEGIS_VT_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        // Заложено в сборку при публикации финального билда (-p:VirusTotalKey=…).
        var fromAssembly = typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "VirusTotalKey")?.Value;
        if (!string.IsNullOrWhiteSpace(fromAssembly))
        {
            return fromAssembly.Trim();
        }

        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "virustotal.key");
            if (File.Exists(path))
            {
                var key = File.ReadAllText(path).Trim();
                if (key.Length > 0)
                {
                    return key;
                }
            }
        }
        catch (Exception)
        {
            // Файл ключа недоступен — онлайн-проверка пойдёт только через Защитник.
        }

        return null;
    }

    /// <summary>
    /// Ключ Gemini. Приоритет: окружение → СВОЙ ключ пользователя (локальный файл) → зашитый при публикации.
    /// Свой ключ важнее зашитого: у каждого пользователя свой лимит (зашитый — общий на всех, кому отдали .exe).
    /// </summary>
    private static string? LoadGeminiKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable("AEGIS_GEMINI_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        try
        {
            if (File.Exists(GeminiUserKeyPath))
            {
                var key = File.ReadAllText(GeminiUserKeyPath).Trim();
                if (key.Length > 0)
                {
                    return key;
                }
            }
        }
        catch (Exception)
        {
            // Свой ключ недоступен — попробуем зашитый.
        }

        var fromAssembly = typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GeminiKey")?.Value;
        return string.IsNullOrWhiteSpace(fromAssembly) ? null : fromAssembly.Trim();
    }

    /// <summary>Путь к личному ключу Gemini пользователя (вводится в программе, хранится только на этом ПК).</summary>
    public static string GeminiUserKeyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "gemini.key");

    /// <summary>Ключ запасной ИИ-модели (Groq/Mistral): окружение → свой локальный файл → заложенный при публикации.</summary>
    private static string? LoadAiKey(string envName, string assemblyKey, string fileName)
    {
        var fromEnv = Environment.GetEnvironmentVariable($"AEGIS_{envName}_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", fileName);
            if (File.Exists(path))
            {
                var key = File.ReadAllText(path).Trim();
                if (key.Length > 0)
                {
                    return key;
                }
            }
        }
        catch (Exception)
        {
            // Свой ключ недоступен — попробуем заложенный.
        }

        var fromAssembly = typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == assemblyKey)?.Value;
        return string.IsNullOrWhiteSpace(fromAssembly) ? null : fromAssembly.Trim();
    }
}
