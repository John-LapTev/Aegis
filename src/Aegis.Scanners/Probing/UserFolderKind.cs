namespace Aegis.Scanners.Probing;

/// <summary>
/// Известная папка профиля пользователя — чтобы подписать «большую папку» простыми словами
/// («Загрузки», «Рабочий стол»…) и понимать, можно ли её безопасно предлагать к очистке.
/// <see cref="Other"/> — обычная папка, показываем по её собственному имени.
/// </summary>
public enum UserFolderKind
{
    /// <summary>Обычная папка (имя показываем как есть).</summary>
    Other,

    /// <summary>Загрузки — единственная пользовательская папка, которую безопасно предлагать чистить.</summary>
    Downloads,

    /// <summary>Рабочий стол.</summary>
    Desktop,

    /// <summary>Документы.</summary>
    Documents,

    /// <summary>Изображения.</summary>
    Pictures,

    /// <summary>Музыка.</summary>
    Music,

    /// <summary>Видео.</summary>
    Videos,

    /// <summary>Данные программ (AppData).</summary>
    AppData,

    /// <summary>Папка облака OneDrive.</summary>
    OneDrive,

    /// <summary>Вся папка пользователя (C:\Users\Имя) — здесь ВСЕ личные файлы, трогать нельзя.</summary>
    UserProfile,
}
