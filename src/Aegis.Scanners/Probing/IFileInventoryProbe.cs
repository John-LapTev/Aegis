namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик списка пользовательских файлов (например, из «Загрузки», «Документы», «Рабочий стол»)
/// с размерами и хэшами. Только ЧИТАЕТ. Windows-реализация — в слое доступа к системе;
/// логика поиска больших/дублей — в <see cref="Files.LargeDuplicateScanner"/>.
/// </summary>
public interface IFileInventoryProbe
{
    /// <summary>Перечислить файлы-кандидаты.</summary>
    Task<IReadOnlyList<FileEntry>> FindAsync(CancellationToken cancellationToken = default);
}
