using Microsoft.Win32;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>Реальный пробник приватности: телеметрия, рекламный ID, реклама в Пуске, персонализация.</summary>
public sealed class PrivacyProbe : IPrivacyProbe
{
    private static readonly (string Service, string Name)[] XboxServices =
    [
        ("XblAuthManager", "Автозапуск: диспетчер аутентификации Xbox Live"),
        ("XblGameSave", "Автозапуск: сохранение игр Xbox Live"),
        ("XboxGipSvc", "Автозапуск: служба геймпадов Xbox"),
        ("XboxNetApiSvc", "Автозапуск: сетевая служба Xbox Live"),
        ("GamingServices", "Автозапуск: игровые службы Xbox (Gaming Services)"),
    ];

    public Task<PrivacySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        // Телеметрия: из политики и из реального параметра (Settings пишет в DataCollection).
        var telemetry = RegistryReader.GetDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry")
                        ?? RegistryReader.GetDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry")
                        ?? RegistryReader.GetDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Microsoft\Windows\DataCollection", "AllowTelemetry");

        bool OnDefault(RegistryHive hive, string subKey, string name) =>
            RegistryReader.GetDword(hive, subKey, name) != 0; // null → включено (значение по умолчанию)

        // Для «политик-выключателей» (где выключение = записать конкретное значение, обычно 1):
        // «активно/есть что отключить», если значение ещё НЕ равно выключающему.
        bool NotSetTo(RegistryHive hive, string subKey, string name, int disableValue) =>
            RegistryReader.GetDword(hive, subKey, name) != disableValue;

        var toggles = new List<PrivacyToggle>
        {
            Toggle("privacy-ad-id", "Включён рекламный идентификатор", "Приложения отслеживают тебя для рекламы",
                "Этот идентификатор позволяет приложениям показывать персональную рекламу и следить за интересами. " +
                "Не опасно — можно выключить ради приватности.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled")),

            Toggle("privacy-start-ads", "Реклама и рекомендации в меню «Пуск»", "Подсказки и реклама приложений",
                "Windows показывает в «Пуске» рекламу и предложения приложений. Не опасно — можно отключить, чтобы не мешали.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled")),

            Toggle("privacy-tailored", "Персонализированная реклама по твоим данным", "Windows подстраивает рекламу под тебя",
                "Windows использует собранные данные, чтобы показывать подходящую рекламу и советы. Не опасно, можно выключить.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled")),

            Toggle("privacy-cortana", "Включён голосовой помощник Кортана", "Кортана работает в фоне",
                "Кортана — голосовой помощник, который многим не нужен и работает в фоне. Можно отключить.",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0,
                OnDefault(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana")),

            Toggle("privacy-websearch", "Поиск в интернете в меню «Пуск»", "Пуск ищет в интернете (Bing)",
                "Когда ищешь в «Пуске», Windows отправляет запрос в интернет (Bing) и показывает веб-результаты. " +
                "Можно оставить только поиск по компьютеру — приватнее и быстрее.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled")),

            Toggle("privacy-activity", "История действий (Timeline)", "Windows запоминает, что ты открывал",
                "Windows ведёт историю действий (какие приложения и файлы ты открывал) и может отправлять её в Microsoft. " +
                "Можно отключить ради приватности.",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0,
                OnDefault(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities")),

            Toggle("privacy-location", "Геолокация (определение местоположения)", "Приложения видят, где ты находишься",
                "Windows и приложения могут определять твоё местоположение. Если тебе это не нужно — можно отключить.",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1,
                RegistryReader.GetDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation") != 1),

            Toggle("privacy-explorer-ads", "Реклама в проводнике Windows", "Подсказки и реклама OneDrive/Office",
                "В проводнике Windows иногда показывает рекламу OneDrive и Office. Можно отключить.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications")),

            // ===== ИИ-слежка (новые функции Windows 2024–2025) =====
            Toggle("privacy-recall", "Windows Recall — тайные снимки экрана", "ИИ запоминает всё, что ты делаешь",
                "Новая функция Windows может тайно делать снимки экрана каждые несколько секунд, чтобы ИИ «вспоминал», " +
                "чем ты занимался. Это серьёзный риск приватности — рекомендуем полностью отключить.",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1,
                NotSetTo(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1)),

            Toggle("privacy-copilot", "Помощник Copilot (ИИ) включён", "Copilot встроен в Windows и работает в фоне",
                "Copilot — ИИ-помощник Microsoft, встроенный в Windows. Многим он не нужен и работает в фоне. Можно отключить.",
                "HKCU", @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1,
                NotSetTo(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)),

            // ===== Больше антирекламы Windows =====
            Toggle("privacy-silent-apps", "Авто-установка «рекомендованных» приложений", "Windows сама ставит рекламные приложения",
                "Windows может молча устанавливать «рекомендованные» (часто рекламные) приложения без твоего ведома. Можно запретить.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled")),

            Toggle("privacy-lockscreen-ads", "Реклама и «факты» на экране блокировки", "Windows Spotlight показывает рекламу при входе",
                "На экране блокировки Windows крутит картинки с рекламой и «интересными фактами». Можно отключить.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled")),

            Toggle("privacy-start-recommend", "Блок «Рекомендуем» в меню «Пуск»", "Пуск показывает рекомендации и предложения",
                "В меню «Пуск» есть блок рекомендаций с предложениями приложений и недавними файлами. Можно убрать.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations")),

            // ===== Приватность (дополнительно) =====
            Toggle("privacy-inking", "Сбор образцов твоего ввода (текст/рукопись)", "Windows собирает то, что ты печатаешь",
                "Windows может собирать образцы твоего ввода — набранный текст и рукописный ввод — для «улучшения распознавания». " +
                "Можно запретить ради приватности.",
                "HKCU", @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1,
                NotSetTo(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1)),

            Toggle("privacy-search-history", "История поиска на устройстве", "Windows запоминает твои поисковые запросы",
                "Windows хранит историю того, что ты искал на компьютере. Можно отключить ради приватности.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDeviceSearchHistoryEnabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDeviceSearchHistoryEnabled")),

            Toggle("privacy-delivery-opt", "Раздача обновлений другим ПК (P2P)", "Твой интернет раздаёт обновления чужим",
                "Windows может использовать твой интернет, чтобы раздавать обновления другим компьютерам в сети и интернете. " +
                "Если хочешь экономить трафик — можно отключить.",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0,
                OnDefault(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode")),

            // Сбор данных совместимости (Appraiser/Inventory). Саму ЗАДАЧУ Windows защищает (нельзя выключить
            // через планировщик), но её сбор отключается ШТАТНОЙ политикой AppCompat\DisableInventory — обратимо.
            Toggle("privacy-appcompat", "Сбор данных совместимости (Appraiser)", "Windows проверяет программы и шлёт данные в Microsoft",
                "Windows фоном собирает сведения об установленных программах «для совместимости» и отправляет их в Microsoft " +
                "(задача Appraiser). Саму задачу Windows выключить не даёт, но её сбор данных отключается штатной настройкой — " +
                "безопасно и обратимо.",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableInventory", 1,
                NotSetTo(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableInventory", 1)),

            // ===== Быт/безопасность =====
            Toggle("settings-show-ext", "Расширения файлов скрыты (риск)", "Не видно, что «.pdf» на самом деле «.exe»",
                "Windows прячет расширения файлов, поэтому вирус «отчёт.pdf.exe» выглядит как безобидный «отчёт.pdf». " +
                "Лучше включить показ расширений — так проще не попасться.",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0,
                OnDefault(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt")),

            Toggle("privacy-gamedvr", "Фоновая запись игр (Xbox GameDVR)", "Windows фоном записывает игровой процесс",
                "Функция Xbox Game Bar постоянно фоном записывает игру (на случай «захватить момент»), что может снижать FPS. " +
                "Если не пользуешься записью — можно отключить.",
                "HKCU", @"System\GameConfigStore", "GameDVR_Enabled", 0,
                OnDefault(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled")),
        };

        var debloat = new List<DebloatItem>();
        foreach (var (service, name) in XboxServices)
        {
            var start = RegistryReader.GetDword(RegistryHive.LocalMachine,
                $@"SYSTEM\CurrentControlSet\Services\{service}", "Start");
            // Существует и не отключена (4 = отключено) — есть что предложить выключить.
            if (start is >= 0 and < 4)
            {
                debloat.Add(new DebloatItem
                {
                    Name = name,
                    Category = "служба Xbox",
                    Enabled = true,
                    ServiceName = service,
                });
            }
        }

        foreach (var (path, name) in ScheduledTaskReader.GetEnabledBloatTasks())
        {
            debloat.Add(new DebloatItem
            {
                Name = name,
                Category = "фоновая задача телеметрии",
                Enabled = true,
                TaskName = path,
            });
        }

        var snapshot = new PrivacySnapshot
        {
            TelemetryLevel = telemetry,
            Toggles = toggles,
            DebloatItems = debloat,
        };

        return Task.FromResult(snapshot);
    }

    private static PrivacyToggle Toggle(
        string id, string title, string detail, string explain,
        string hive, string subKey, string valueName, int disableValue, bool enabled) =>
        new()
        {
            Id = id,
            Title = title,
            Detail = detail,
            Explain = explain,
            Hive = hive,
            SubKey = subKey,
            ValueName = valueName,
            DisableValue = disableValue,
            Enabled = enabled,
        };
}
