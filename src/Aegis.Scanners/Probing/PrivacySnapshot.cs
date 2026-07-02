namespace Aegis.Scanners.Probing;

/// <summary>Снимок настроек приватности и «лишнего» в Windows (read-only).</summary>
public sealed record PrivacySnapshot
{
    /// <summary>
    /// Уровень телеметрии (AllowTelemetry): 0/1 — минимум (хорошо), 2/3 — расширенная (можно снизить),
    /// null — значение не задано. Влияет на статус пункта телеметрии (OK или «снизить»).
    /// </summary>
    public required int? TelemetryLevel { get; init; }

    /// <summary>Переключатели приватности (рекламный ID, реклама в Пуске, Кортана, поиск, история, геолокация…).</summary>
    public required IReadOnlyList<PrivacyToggle> Toggles { get; init; }

    /// <summary>Каталог заведомо лишних элементов (фоновые задачи/службы/функции) и их состояние.</summary>
    public required IReadOnlyList<DebloatItem> DebloatItems { get; init; }
}
