namespace Aegis.Core.Models;

/// <summary>
/// Установленная программа (из веток «Uninstall» реестра) — для раздела «Удаление программ»: показать список,
/// удалить штатным деинсталлятором и вычистить остатки (пустая папка установки, осиротевшая запись реестра).
/// </summary>
public sealed record InstalledProgram
{
    /// <summary>Название программы (DisplayName).</summary>
    public required string Name { get; init; }

    /// <summary>Издатель (Publisher), если указан.</summary>
    public string? Publisher { get; init; }

    /// <summary>Версия (DisplayVersion), если указана.</summary>
    public string? Version { get; init; }

    /// <summary>Папка установки (InstallLocation), если указана — для проверки остатков после удаления.</summary>
    public string? InstallLocation { get; init; }

    /// <summary>Команда штатного удаления (UninstallString).</summary>
    public string? UninstallCommand { get; init; }

    /// <summary>Команда тихого удаления без окон (QuietUninstallString), если есть — предпочтительнее.</summary>
    public string? QuietUninstallCommand { get; init; }

    /// <summary>Приблизительный размер на диске в байтах (EstimatedSize из реестра, в КБ × 1024); 0 — неизвестно.</summary>
    public long EstimatedSizeBytes { get; init; }

    /// <summary>Дата установки (InstallDate из реестра, YYYYMMDD), если удалось прочитать — для сортировки.</summary>
    public DateOnly? InstallDate { get; init; }

    /// <summary>Путь к значку программы (DisplayIcon из реестра, вида «C:\App\app.exe,0» или «…\icon.ico»); null — нет.</summary>
    public string? IconPath { get; init; }

    /// <summary>Системная/скрытая программа (компонент Windows, обновление и т.п.) — показывается только по запросу, с пометкой.</summary>
    public bool IsSystem { get; init; }

    /// <summary>Полный путь ветки реестра этой программы (для вычистки осиротевшей записи после удаления).</summary>
    public required string RegistryKeyPath { get; init; }

    /// <summary>Можно ли удалить (есть команда удаления). Иначе — только показываем.</summary>
    public bool CanUninstall => !string.IsNullOrWhiteSpace(UninstallCommand) || !string.IsNullOrWhiteSpace(QuietUninstallCommand);
}
