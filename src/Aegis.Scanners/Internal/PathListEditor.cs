namespace Aegis.Scanners.Internal;

/// <summary>
/// Правка переменной среды <c>Path</c> — списка папок, в которых Windows ищет программы. Записи от удалённых
/// программ остаются в нём навсегда и замедляют запуск команд (система проверяет каждую папку по очереди).
///
/// Здесь только чистые функции: разбор списка, удаление записи и защита от порчи. Порча переменной Path —
/// это неработающие команды по всей системе, поэтому пустой результат считается ошибкой и никогда не пишется.
/// </summary>
public static class PathListEditor
{
    /// <summary>Разобрать значение переменной на отдельные папки (пустые и повторяющиеся пробелы отбрасываются).</summary>
    public static IReadOnlyList<string> Split(string? value) =>
        (value ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>
    /// Убрать одну запись из списка. Возвращает новое значение переменной или null, если убирать нечего либо
    /// результат оказался бы пустым (такое значение писать нельзя — сломается запуск программ).
    /// </summary>
    public static string? Remove(string? currentValue, string entryToRemove)
    {
        if (string.IsNullOrWhiteSpace(entryToRemove))
        {
            return null;
        }

        var entries = Split(currentValue);
        var kept = entries
            .Where(entry => !SamePath(entry, entryToRemove))
            .ToList();

        if (kept.Count == entries.Count)
        {
            return null; // записи уже нет — значит правка не нужна
        }

        return kept.Count == 0 ? null : string.Join(';', kept);
    }

    /// <summary>Записи, ведущие в несуществующие папки (проверка существования — снаружи, чтобы тестировать логику).</summary>
    public static IReadOnlyList<string> FindMissing(string? value, Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);

        var missing = new List<string>();
        foreach (var entry in Split(value))
        {
            // Записи с переменными (%JAVA_HOME%\bin) разворачивает система — проверять их «как есть» нельзя.
            if (entry.Contains('%'))
            {
                continue;
            }

            // Сетевые пути (\\сервер\папка) могут быть просто недоступны сейчас — это не повод их удалять.
            if (entry.StartsWith(@"\\", StringComparison.Ordinal))
            {
                continue;
            }

            if (!directoryExists(entry))
            {
                missing.Add(entry);
            }
        }

        return missing;
    }

    /// <summary>Сравнение путей: без учёта регистра и завершающего слэша (Windows считает их одинаковыми).</summary>
    private static bool SamePath(string left, string right) =>
        string.Equals(left.TrimEnd('\\', '/'), right.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
