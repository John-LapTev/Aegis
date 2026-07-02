namespace Aegis.Scanners.Internal;

/// <summary>Простые эвристики по пути файла (без обращения к ФС) для оценки подозрительности.</summary>
internal static class PathHeuristics
{
    // Места, откуда «нормальные» программы обычно не запускаются автоматически —
    // там часто прячется вредоносное ПО. «Загрузки» намеренно НЕ включены: туда
    // качают легальные инсталляторы/портативные программы — иначе много ложных тревог.
    // \temp\ и \tmp\ уже покрывают все временные папки (AppData\Local\Temp, Windows\Temp, ProgramData\Temp
    // — каждая содержит «\temp\»), поэтому отдельные фрагменты для них не нужны.
    private static readonly string[] SuspiciousFragments =
    [
        @"\temp\",
        @"\tmp\",
    ];

    /// <summary>Похоже ли расположение на «подозрительное» (только временные папки; «Загрузки» намеренно исключены — см. список выше).</summary>
    public static bool IsSuspiciousLocation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('/', '\\').ToLowerInvariant();
        foreach (var fragment in SuspiciousFragments)
        {
            if (normalized.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
