namespace Aegis.Scanners.Internal;

/// <summary>
/// Распознавание «LOLBin» — злоупотребления подписанными системными утилитами Windows
/// (powershell, mshta, rundll32, regsvr32, certutil, bitsadmin, wscript) для скрытого запуска кода.
/// Сам бинарь подписан Microsoft, поэтому проверка подписи его пропускает — ловим по командной строке.
/// </summary>
internal static class LolBinHeuristics
{
    /// <summary>
    /// Возвращает понятную причину, если командная строка похожа на злоупотребление системной
    /// утилитой; иначе null. Эвристика консервативна — реагирует на явные «download &amp; execute»-паттерны.
    /// </summary>
    public static string? DetectAbuse(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var c = command.ToLowerInvariant();

        if (c.Contains("powershell", StringComparison.Ordinal) && ContainsAny(c,
                "-enc", "-ec ", "encodedcommand", "-w hidden", "-windowstyle hidden",
                "downloadstring", "downloadfile", "iex", "invoke-expression", "frombase64string", "-nop"))
        {
            return "запуск PowerShell со скрытой или зашифрованной командой";
        }

        if (c.Contains("mshta", StringComparison.Ordinal) && ContainsAny(c, "http", "javascript:", "vbscript:"))
        {
            return "запуск mshta с кодом из интернета";
        }

        if (c.Contains("rundll32", StringComparison.Ordinal) && ContainsAny(c, "javascript:", "http", "mshtml"))
        {
            return "запуск rundll32 с кодом или из интернета";
        }

        if (c.Contains("regsvr32", StringComparison.Ordinal) && ContainsAny(c, "/i:http", "scrobj", "http"))
        {
            return "регистрация кода через regsvr32 из интернета";
        }

        if (c.Contains("certutil", StringComparison.Ordinal) && ContainsAny(c, "urlcache", "-decode", "http"))
        {
            return "загрузка или декодирование файла через certutil";
        }

        if (c.Contains("bitsadmin", StringComparison.Ordinal) && c.Contains("transfer", StringComparison.Ordinal))
        {
            return "скрытая загрузка файла через bitsadmin";
        }

        if (ContainsAny(c, "wscript", "cscript") && ContainsAny(c, ".vbs", ".js", "http"))
        {
            return "запуск скрипта через wscript/cscript";
        }

        // Ниже — расширение по мотивам каталога LOLBAS (clean-room, свои консервативные паттерны):
        // реагируем только на явный приём злоупотребления, не на сам факт наличия утилиты.
        if (c.Contains("wmic", StringComparison.Ordinal) && c.Contains("process call create", StringComparison.Ordinal))
        {
            return "скрытый запуск программы через wmic";
        }

        if (ContainsAny(c, "installutil", "regasm", "regsvcs") && ContainsAny(c, "http", "/u "))
        {
            return "запуск кода в обход через .NET-утилиту (installutil/regasm/regsvcs)";
        }

        if (c.Contains("cmstp", StringComparison.Ordinal) && c.Contains(".inf", StringComparison.Ordinal)
                                                          && ContainsAny(c, "/s", "/ni"))
        {
            return "запуск кода через cmstp";
        }

        if (c.Contains("msiexec", StringComparison.Ordinal) && c.Contains("http", StringComparison.Ordinal)
                                                            && ContainsAny(c, "/q", "/quiet"))
        {
            return "тихая установка пакета из интернета через msiexec";
        }

        if (c.Contains("forfiles", StringComparison.Ordinal) && c.Contains("/c", StringComparison.Ordinal)
                                                             && ContainsAny(c, "cmd", ".exe"))
        {
            return "запуск программы через forfiles";
        }

        if (c.Contains("msdt", StringComparison.Ordinal) && ContainsAny(c, "ms-msdt", "/af"))
        {
            return "эксплойт msdt (Follina)";
        }

        return null;
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
