namespace Aegis.Core.Models;

/// <summary>Вид бэкапа, созданного перед обратимой правкой (ADR 0002).</summary>
public enum BackupKind
{
    /// <summary>Точка восстановления Windows (System Restore).</summary>
    SystemRestorePoint,

    /// <summary>Экспорт затронутой ветки реестра.</summary>
    RegistryExport,

    /// <summary>Карантин файла перед удалением/перемещением (мусор, угрозы).</summary>
    FileQuarantine,

    /// <summary>Снимок изменённой системной настройки/службы.</summary>
    SettingSnapshot,
}
