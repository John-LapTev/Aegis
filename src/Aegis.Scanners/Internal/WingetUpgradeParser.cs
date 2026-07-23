namespace Aegis.Scanners.Internal;

/// <summary>
/// Разбор списка доступных обновлений программ (<c>winget upgrade</c>). Вывод — таблица с колонками
/// «Название | Идентификатор | Версия | Доступно | Источник»; ширина колонок плавает, заголовки переведены,
/// поэтому опираемся на позиции колонок из строки-заголовка, а не на её текст.
///
/// Чистая функция — проверяется тестами на любой ОС.
/// </summary>
public static class WingetUpgradeParser
{
    /// <summary>
    /// Программы, для которых есть новая версия. Строки-разделители, шапка и итоговые сообщения отбрасываются.
    /// </summary>
    public static IReadOnlyList<AvailableUpgrade> Parse(string output)
    {
        var result = new List<AvailableUpgrade>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        var lines = output.Replace("\r\n", "\n").Split('\n');

        // Заголовок таблицы — строка прямо над разделителем из дефисов.
        var separatorIndex = Array.FindIndex(lines, IsSeparator);
        if (separatorIndex <= 0)
        {
            return result;
        }

        var header = lines[separatorIndex - 1];
        var columns = FindColumnStarts(header);
        if (columns.Count < 4)
        {
            return result;
        }

        for (var i = separatorIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Вторая таблица «требуют явного указания» рисует СВОЙ разделитель из дефисов. У первой таблицы
            // внутренних разделителей нет, поэтому первый же встреченный дефис-разделитель = конец нашей
            // таблицы. Дальше идут закреплённые пакеты, которые обновлять не нужно (найдено аудитом 2026-07-23).
            if (IsSeparator(line))
            {
                break;
            }

            // Хвост вывода — фразы вида «Найдено обновлений: 5» без колонок.
            if (line.Length <= columns[1])
            {
                continue;
            }

            var name = Slice(line, columns[0], columns[1]);
            var id = Slice(line, columns[1], columns[2]);
            var current = Slice(line, columns[2], columns[3]);
            var available = columns.Count > 4 ? Slice(line, columns[3], columns[4]) : Slice(line, columns[3], line.Length);

            if (name.Length == 0 || id.Length == 0 || available.Length == 0)
            {
                continue;
            }

            // Идентификатор пакета winget не содержит пробелов (7zip.7zip, Microsoft.Edge, XPDNZ...).
            // Пояснительный текст между таблицами («The following packages…»), нарезанный по колонкам, даёт
            // фрагмент с пробелами — так отсекаем мусорные строки независимо от языка системы.
            if (id.Contains(' '))
            {
                continue;
            }

            // Строки без реальной версии («Unknown», «<неизвестно>») пропускаем: обновлять вслепую не будем.
            if (available.Contains('<') || available.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Настоящая версия всегда содержит цифру. Заголовок второй таблицы («Доступно»/«Available») и
            // пояснительный текст цифр не содержат — так отсекаем их независимо от языка системы
            // (баг найден аудитом 2026-07-23).
            if (!available.Any(char.IsDigit))
            {
                continue;
            }

            result.Add(new AvailableUpgrade
            {
                Name = name,
                Id = id,
                CurrentVersion = current,
                AvailableVersion = available,
            });
        }

        return result;
    }

    /// <summary>Строка-разделитель таблицы: только дефисы (winget рисует её после заголовка).</summary>
    private static bool IsSeparator(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 10 && trimmed.All(c => c == '-');
    }

    /// <summary>Позиции, с которых начинаются колонки (по заголовку таблицы).</summary>
    private static List<int> FindColumnStarts(string header)
    {
        var starts = new List<int>();
        var inGap = true;

        for (var i = 0; i < header.Length; i++)
        {
            if (header[i] == ' ')
            {
                inGap = true;
                continue;
            }

            if (inGap)
            {
                starts.Add(i);
                inGap = false;
            }
        }

        return starts;
    }

    private static string Slice(string line, int start, int end)
    {
        if (start >= line.Length)
        {
            return string.Empty;
        }

        var stop = Math.Min(end, line.Length);
        return line[start..stop].Trim();
    }
}

/// <summary>Программа, для которой доступна новая версия.</summary>
public sealed record AvailableUpgrade
{
    public required string Name { get; init; }
    public required string Id { get; init; }
    public required string CurrentVersion { get; init; }
    public required string AvailableVersion { get; init; }
}
