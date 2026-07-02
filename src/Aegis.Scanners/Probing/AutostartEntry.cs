namespace Aegis.Scanners.Probing;

/// <summary>Элемент автозапуска (read-only снимок для анализа).</summary>
public sealed record AutostartEntry
{
    /// <summary>Отображаемое имя записи.</summary>
    public required string Name { get; init; }

    /// <summary>Команда/путь, который запускается.</summary>
    public required string Command { get; init; }

    /// <summary>Где прописан автозапуск (категория источника).</summary>
    public required AutostartLocation Location { get; init; }

    /// <summary>
    /// Точный источник записи — различитель для устойчивого Id: полный путь ключа реестра
    /// (например, <c>HKLM\...\Run</c> vs <c>HKCU\...\Run</c>), путь задачи планировщика или папки автозагрузки.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>Статус цифровой подписи исполняемого файла.</summary>
    public required SignatureStatus Signature { get; init; }

    /// <summary>Издатель из подписи (если есть).</summary>
    public string? Publisher { get; init; }

    /// <summary>
    /// Структурные параметры для отключения автозапуска (координаты ключа реестра или путь файла).
    /// Заполняет пробник, читает фабрика правок. Необязательно.
    /// </summary>
    public IReadOnlyDictionary<string, string>? FixData { get; init; }
}
