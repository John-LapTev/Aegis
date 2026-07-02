using Aegis.Scanners.Probing;

namespace Aegis.System.Internal;

/// <summary>
/// Белый список заведомо «лишних» UWP-приложений (промо-игры/демо/редко используемое), которые безопасно
/// предложить удалить. Сопоставление по префиксу имени пакета. Консервативно: НЕ трогаем нужное (Калькулятор,
/// Фото, Магазин, Терминал, Почта/Календарь, Phone Link и т.п.). Чистая логика — тестируется на любой ОС.
/// </summary>
internal static class AppxBloatCatalog
{
    private static readonly (string Prefix, string Name, string Category)[] Bloat =
    [
        ("king.com.", "Игры King (Candy Crush и подобные)", "промо-игра"),
        ("Microsoft.BingNews", "Новости (Bing News)", "промо-приложение"),
        ("Microsoft.BingWeather", "Погода (Bing)", "промо-приложение"),
        ("Microsoft.MicrosoftSolitaireCollection", "Коллекция пасьянсов", "промо-игра"),
        ("Microsoft.ZuneMusic", "Groove Музыка", "медиа-приложение"),
        ("Microsoft.ZuneVideo", "Кино и ТВ", "медиа-приложение"),
        ("Microsoft.GetHelp", "Техподдержка (Get Help)", "промо-приложение"),
        ("Microsoft.Getstarted", "Советы (Tips)", "промо-приложение"),
        ("Microsoft.3DBuilder", "3D Builder", "редко используемое"),
        ("Microsoft.Microsoft3DViewer", "Просмотр 3D", "редко используемое"),
        ("Microsoft.MixedReality.Portal", "Портал смешанной реальности", "редко используемое"),
        ("Microsoft.WindowsFeedbackHub", "Центр отзывов", "промо-приложение"),
        ("Microsoft.MicrosoftOfficeHub", "Реклама Office (Get Office)", "промо-приложение"),
        ("Microsoft.People", "Люди (People)", "редко используемое"),
        ("Microsoft.SkypeApp", "Skype (встроенный)", "промо-приложение"),
        ("Clipchamp.Clipchamp", "Clipchamp (видеоредактор)", "промо-приложение"),
        ("Microsoft.Windows.DevHome", "Dev Home (для разработчиков)", "редко используемое"),
        ("Microsoft.PowerAutomateDesktop", "Power Automate (автоматизация)", "редко используемое"),
        ("Microsoft.MicrosoftJournal", "Журнал (Journal)", "редко используемое"),
        ("Microsoft.BingSearch", "Поиск Bing (встроенный)", "промо-приложение"),
        ("Disney.", "Disney Magic Kingdoms (промо-игра)", "промо-игра"),
        ("Microsoft.OutlookForWindows", "Новый Outlook (рекламный)", "промо-приложение"),
    ];

    /// <summary>Отфильтровать из списка полных имён пакетов те, что входят в белый список «лишнего».</summary>
    public static IReadOnlyList<AppxApp> Match(IEnumerable<string> packageFullNames)
    {
        var result = new List<AppxApp>();
        foreach (var raw in packageFullNames)
        {
            var packageFullName = raw.Trim();
            if (packageFullName.Length == 0)
            {
                continue;
            }

            foreach (var (prefix, name, category) in Bloat)
            {
                if (packageFullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new AppxApp { PackageFullName = packageFullName, Name = name, Category = category });
                    break;
                }
            }
        }

        return result;
    }
}
