using System;
using System.Collections.Generic;
using System.Linq;

namespace Aegis.Core;

/// <summary>
/// Разбор ответа ИИ про «дубли распространяемых пакетов» и БЕЗОПАСНОЕ сопоставление помеченных к удалению имён с
/// реально установленными. Ключевое требование (аудит 2026-07-03): НИКОГДА не перепутать разрядность (x86 vs x64) и
/// при любой неоднозначности НЕ помечать пакет к удалению (fail-safe: лучше не удалить, чем удалить не то). Чистая
/// строковая логика без зависимостей — вынесена из VM, чтобы покрыть тестами.
/// </summary>
public static class RedistDeletionMatcher
{
    private const string DeleteMarker = "DEL::";

    /// <summary>
    /// Делит ответ ИИ на пояснение (для человека) и список имён пакетов к удалению. Строкой к удалению считается
    /// ТОЛЬКО строка, начинающаяся (после обрезки маркеров списка) с «DEL::» — чтобы токен в середине прозы не ловился.
    /// </summary>
    public static (string Explanation, IReadOnlyList<string> Deletions) SplitDeletions(string? aiAnswer)
    {
        var deletions = new List<string>();
        var textLines = new List<string>();

        foreach (var rawLine in (aiAnswer ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('-', '*', '•', ' ').Trim();
            if (line.StartsWith(DeleteMarker, StringComparison.OrdinalIgnoreCase))
            {
                var name = line[DeleteMarker.Length..].Trim().Trim('*', '_', '`', ' ').Trim();
                if (name.Length > 0)
                {
                    deletions.Add(name);
                }
            }
            else
            {
                textLines.Add(rawLine);
            }
        }

        return (string.Join("\n", textLines).Trim(), deletions);
    }

    /// <summary>
    /// Находит РОВНО одну установленную запись, соответствующую имени из ответа ИИ. Возвращает её имя или null, если
    /// совпадений нет ИЛИ их несколько (неоднозначно) ИЛИ разрядность не совпадает. Разрядность (x86/x64) обязана
    /// совпадать — это защита от подмены «удалить x86» на реально стоящий x64 (и наоборот).
    /// </summary>
    public static string? MatchInstalled(string? aiName, IReadOnlyList<string> installedNames)
    {
        var ai = (aiName ?? string.Empty).Trim();
        if (ai.Length == 0)
        {
            return null;
        }

        // 1) Точное совпадение — самый надёжный случай.
        var exact = installedNames.FirstOrDefault(n => n.Trim().Equals(ai, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        // 2) Префиксное совпадение в любую сторону (ИИ мог дописать/опустить хвост версии), но с сохранением имени.
        var candidates = installedNames
            .Where(n =>
            {
                var t = n.Trim();
                return t.StartsWith(ai, StringComparison.OrdinalIgnoreCase)
                       || ai.StartsWith(t, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        // 3) Разрядность обязана совпадать. Если у имени ИИ разрядность не указана, а у кандидатов есть — это
        //    неоднозначно (какой из x86/x64?) → отбрасываем, чтобы не удалить не тот.
        var aiArch = Architecture(ai);
        candidates = candidates.Where(n => Architecture(n) == aiArch).ToList();

        // Единственный кандидат — берём; иначе fail-safe null (лучше не пометить, чем пометить не то).
        return candidates.Count == 1 ? candidates[0] : null;
    }

    /// <summary>Разрядность из имени пакета: «x64» / «x86» / null (если не указана).</summary>
    private static string? Architecture(string name)
    {
        if (name.Contains("x64", StringComparison.OrdinalIgnoreCase))
        {
            return "x64";
        }

        if (name.Contains("x86", StringComparison.OrdinalIgnoreCase))
        {
            return "x86";
        }

        return null;
    }
}
