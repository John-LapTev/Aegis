namespace Aegis.System.Internal;

/// <summary>Извлечение пути к исполняемому файлу из командной строки автозапуска.</summary>
internal static class CommandLine
{
    public static string ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return string.Empty;
        }

        var expanded = Environment.ExpandEnvironmentVariables(command.Trim());

        if (expanded.StartsWith('"'))
        {
            var end = expanded.IndexOf('"', 1);
            return end > 1 ? expanded[1..end] : expanded;
        }

        // Без кавычек: путь до первого пробела (грубо, но для эвристик достаточно).
        var space = expanded.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? expanded[..space] : expanded;
    }
}
