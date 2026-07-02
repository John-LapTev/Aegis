namespace Aegis.Core.Models;

/// <summary>
/// Снимок прогресса сканирования для отображения в UI (кольцо/полоса + счётчики).
/// </summary>
public sealed record ScanProgress
{
    /// <summary>
    /// Группа, которая сканируется прямо сейчас. При <see cref="IsComplete"/> = true
    /// (финальный отчёт) поле неинформативно — UI должен ориентироваться на <see cref="IsComplete"/>.
    /// </summary>
    public required ScanGroup Current { get; init; }

    /// <summary>Сколько групп уже завершено.</summary>
    public required int CompletedGroups { get; init; }

    /// <summary>Всего групп в этом проходе.</summary>
    public required int TotalGroups { get; init; }

    /// <summary>Сколько находок собрано на текущий момент.</summary>
    public required int FindingsSoFar { get; init; }

    /// <summary>Скан завершён (финальный отчёт). Для промежуточных отчётов — false.</summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Результат сканера, который ТОЛЬКО ЧТО завершился (для пошагового наполнения вкладок — чтобы раздел
    /// заполнялся сразу, как проверен, а не в самом конце всей проверки). Null — для стартовых/финальных отчётов.
    /// </summary>
    public ScanResult? JustCompleted { get; init; }

    /// <summary>Все сканеры этой группы завершены — раздел можно пометить «проверен» (для шкалы по блокам при параллельном скане).</summary>
    public bool GroupComplete { get; init; }

    /// <summary>Доля выполнения 0..1 (для заливки прогресс-кольца).</summary>
    public double Fraction => TotalGroups == 0 ? 0 : (double)CompletedGroups / TotalGroups;
}
