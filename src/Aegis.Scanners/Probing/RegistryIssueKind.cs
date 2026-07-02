namespace Aegis.Scanners.Probing;

/// <summary>Тип проблемы в реестре.</summary>
public enum RegistryIssueKind
{
    /// <summary>Запись об удалении программы, которой уже нет на компьютере.</summary>
    OrphanedUninstallEntry,

    /// <summary>Значение ссылается на несуществующий файл.</summary>
    MissingFileReference,

    /// <summary>Запись автозапуска указывает на отсутствующий файл.</summary>
    InvalidStartupReference,

    /// <summary>Пустой/битый ключ автозапуска.</summary>
    EmptyAutostartKey,
}
