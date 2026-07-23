using Aegis.Core.Models;

namespace Aegis.Scanners.Internal;

/// <summary>
/// Каталог ограничений Windows, которые чаще всего оставляют после себя чужие «оптимизаторы», активаторы и
/// вирусы. Такие настройки живут в ветках <c>Policies</c> и имеют приоритет над обычными: человек видит, что
/// «Защитник не включается», «обновления не работают», «диспетчер задач заблокирован» — и считает, что Windows
/// сломалась, хотя достаточно убрать забытую политику.
///
/// Это ДАННЫЕ: один пункт — одно значение реестра с понятным объяснением, чем оно мешает. Идея сканирования
/// подсмотрена в Sophia Script (там политики сверяются с шаблонами ADMX); у нас — выверенный список тех,
/// что реально вредят обычному пользователю, чтобы не пугать человека сотней служебных записей.
/// </summary>
public static class PolicyCatalog
{
    /// <summary>Все проверяемые ограничения.</summary>
    public static readonly IReadOnlyList<PolicyRule> Rules =
    [
        // ─── Защита ───────────────────────────────────────────────────────────
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1, Severity.Danger,
            "Защитник Windows отключён навсегда",
            "Кто-то запретил Защитнику работать через системную политику — не просто выключил, а запретил включаться. " +
            "Так делают активаторы пиратских программ и вирусы, чтобы их не удалили. Компьютер остаётся без антивируса. " +
            "Уберём запрет — Защитник снова сможет работать."),
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1, Severity.Danger,
            "Постоянная защита Защитника запрещена",
            "Запрещена проверка файлов в реальном времени: вирус может спокойно запуститься, и его никто не остановит. " +
            "Уберём запрет."),
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0, Severity.Warning,
            "Проверка скачанных файлов (SmartScreen) отключена",
            "SmartScreen предупреждает, когда скачанная программа выглядит опасной. Сейчас проверка отключена запретом. " +
            "Вернём её — это одна из самых полезных бесплатных защит Windows."),

        // ─── Обновления ───────────────────────────────────────────────────────
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1, Severity.Warning,
            "Автообновления Windows запрещены политикой",
            "Обновления Windows закрывают дыры, через которые проникают вирусы. Их запретили — скорее всего, чужой " +
            "«ускоритель» или активатор. Уберём запрет, обновления снова будут приходить."),
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DisableWindowsUpdateAccess", 1, Severity.Warning,
            "Доступ к Центру обновления заблокирован",
            "Заблокирована сама страница обновлений: даже вручную проверить обновления не выйдет. Уберём блокировку."),

        // ─── Восстановление системы ───────────────────────────────────────────
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore", "DisableSR", 1, Severity.Danger,
            "Восстановление системы запрещено",
            "Точки восстановления — способ вернуть Windows назад, если что-то сломалось. Их запретили, поэтому " +
            "откатиться после неудачных изменений не получится. Уберём запрет — защита снова заработает."),
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore", "DisableConfig", 1, Severity.Warning,
            "Настройка восстановления системы заблокирована",
            "Нельзя включить или настроить защиту дисков. Уберём блокировку."),

        // ─── Инструменты пользователя ─────────────────────────────────────────
        new("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr", 1, Severity.Danger,
            "Диспетчер задач заблокирован",
            "Диспетчер задач (Ctrl+Shift+Esc) — то, чем смотрят, какая программа грузит компьютер. Его заблокировали. " +
            "Так почти всегда делают вирусы, чтобы их нельзя было закрыть. Разблокируем."),
        new("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools", 1, Severity.Warning,
            "Редактор реестра заблокирован",
            "Заблокирован редактор реестра — обычно это делают, чтобы нельзя было убрать чужие настройки. Разблокируем."),
        new("HKCU", @"SOFTWARE\Policies\Microsoft\Windows\System", "DisableCMD", 1, Severity.Info,
            "Командная строка заблокирована",
            "Заблокирована командная строка. Обычному человеку она не нужна каждый день, но блокировку обычно ставит " +
            "не он сам — это след чужой программы. Можно вернуть."),
        new("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoControlPanel", 1, Severity.Warning,
            "Панель управления и Параметры заблокированы",
            "Нельзя открыть настройки Windows. Это ограничение поставила какая-то программа — уберём его."),
        new("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun", 1, Severity.Info,
            "Окно «Выполнить» заблокировано",
            "Заблокировано окно «Выполнить» (Win+R). Ограничение чужое — можно снять."),
        new("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDrives", null, Severity.Warning,
            "Часть дисков скрыта от проводника",
            "Кто-то скрыл диски в «Этом компьютере» — они есть, но не показываются. Уберём скрытие."),
        new("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoFolderOptions", 1, Severity.Info,
            "Настройки папок заблокированы",
            "Нельзя менять параметры проводника (например, показ расширений файлов). Уберём ограничение."),

        // ─── Защита учётной записи ────────────────────────────────────────────
        new("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0, Severity.Danger,
            "Контроль учётных записей (UAC) отключён политикой",
            "UAC спрашивает разрешение, когда программа хочет изменить систему. Его отключили — теперь любая программа " +
            "меняет что угодно молча. Включим обратно (потребуется перезагрузка)."),
        new("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 0, Severity.Warning,
            "Запрос прав администратора отключён",
            "Программы получают полные права без вопроса. Вернём подтверждение."),

        // ─── Установка программ ───────────────────────────────────────────────
        new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Installer", "DisableMSI", null, Severity.Warning,
            "Установка программ ограничена",
            "Стоит запрет на установку программ через установщик Windows — из-за него обычные программы могут не " +
            "ставиться с непонятной ошибкой. Уберём запрет."),
    ];

    /// <summary>Одно проверяемое ограничение Windows.</summary>
    /// <param name="Hive">Куст реестра: <c>HKLM</c> или <c>HKCU</c>.</param>
    /// <param name="SubKey">Путь к ключу.</param>
    /// <param name="ValueName">Имя значения.</param>
    /// <param name="BadValue">
    /// Значение, которое считается вредным. <c>null</c> — вредно само наличие значения (любое ненулевое).
    /// </param>
    /// <param name="Severity">Насколько это опасно для человека.</param>
    /// <param name="Title">Заголовок находки простыми словами.</param>
    /// <param name="Explain">Объяснение: что это, чем мешает, что даст исправление.</param>
    public sealed record PolicyRule(
        string Hive,
        string SubKey,
        string ValueName,
        int? BadValue,
        Severity Severity,
        string Title,
        string Explain);

    /// <summary>Совпадает ли прочитанное значение с «вредным» по правилу.</summary>
    public static bool IsBad(PolicyRule rule, int? actualValue)
    {
        if (actualValue is not int value)
        {
            return false; // значения нет — ограничения нет
        }

        // BadValue=null означает «любое ненулевое значение вредно» (например, маска скрытых дисков).
        return rule.BadValue is int bad ? value == bad : value != 0;
    }
}
