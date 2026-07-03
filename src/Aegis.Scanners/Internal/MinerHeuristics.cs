using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Internal;

/// <summary>
/// Общие эвристики «портрета скрытого майнера» — используются и сканером процессов (<c>MinerBehaviorScanner</c>),
/// и фоновым стражем (<c>GuardEvaluator</c>), чтобы логика не дублировалась.
/// </summary>
internal static class MinerHeuristics
{
    /// <summary>Порог загрузки CPU (% от всей мощности), ниже которого процесс не считаем возможным майнером.</summary>
    public const double CpuGate = 25d;

    /// <summary>Программа без подтверждённой подписи (неподписанная или подпись не читается).</summary>
    public static bool IsUntrusted(SignatureStatus signature) => signature != SignatureStatus.Signed;

    /// <summary>Файл лежит в скрытой служебной папке (временной или в профиле пользователя), где майнеры любят прятаться.</summary>
    public static bool IsStealthPath(string? path)
    {
        if (PathHeuristics.IsSuspiciousLocation(path))
        {
            return true; // \temp\ / \tmp\
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('/', '\\').ToLowerInvariant();
        return normalized.Contains(@"\appdata\", StringComparison.Ordinal)
               || normalized.Contains(@"\programdata\", StringComparison.Ordinal);
    }

    /// <summary>Имя файла выглядит «случайным» (чистый hex-хэш или длинная строка без гласных) — маскировка.</summary>
    public static bool LooksRandomName(string processName)
    {
        var name = processName;
        var dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            name = name[..dot];
        }

        name = name.ToLowerInvariant();
        if (name.Length < 8)
        {
            return false;
        }

        // Чистый шестнадцатеричный «хэш» (a-f, 0-9) длиной 8+ — очень характерно для дропперов/майнеров.
        if (name.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
        {
            return true;
        }

        // Длинная строка вообще без гласных — не похоже на нормальное название программы.
        return name.Length >= 12 && !name.Any(static c => c is 'a' or 'e' or 'i' or 'o' or 'u' or 'y');
    }
}
