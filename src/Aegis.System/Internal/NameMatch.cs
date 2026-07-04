namespace Aegis.System.Internal;

/// <summary>
/// Сопоставление имени программы в тексте по границам слова — общий помощник для чистки остатков и поиска установленной
/// программы. «Rave» ловится в «Rave.exe» / «C:\Rave\…», но НЕ внутри «Braverman»/«Brave» (иначе снесли бы чужое).
/// </summary>
internal static class NameMatch
{
    /// <summary>Упоминается ли <paramref name="name"/> в <paramref name="text"/> как ОТДЕЛЬНОЕ слово (границы — не буквы/цифры). Регистр не важен.</summary>
    public static bool ReferencesName(string? text, string? name)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(name))
        {
            return false;
        }

        var from = 0;
        while (from <= text.Length - name.Length)
        {
            var idx = text.IndexOf(name, from, global::System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                break;
            }

            var before = idx == 0 ? ' ' : text[idx - 1];
            var afterIndex = idx + name.Length;
            var after = afterIndex >= text.Length ? ' ' : text[afterIndex];
            if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after))
            {
                return true;
            }

            from = idx + 1;
        }

        return false;
    }
}
