namespace Aegis.Core.Models;

/// <summary>Снимок оперативной памяти системы (реальные цифры, без выдумки).</summary>
public sealed record MemorySnapshot
{
    public required long TotalBytes { get; init; }
    public required long AvailableBytes { get; init; }
    public long UsedBytes => TotalBytes - AvailableBytes;
    public int UsedPercent => TotalBytes > 0 ? (int)(UsedBytes * 100 / TotalBytes) : 0;
}

/// <summary>
/// Фоновый процесс (или группа одноимённых), который можно безопасно закрыть, чтобы освободить память
/// (обновлятор/помощник). Одинаковые по смыслу процессы объединяются в одну строку (сумма памяти + все PID).
/// </summary>
public sealed record OptimizableProcess
{
    /// <summary>Все PID этой группы (одноимённые процессы объединены — не путать пользователя дублями).</summary>
    public required IReadOnlyList<int> ProcessIds { get; init; }
    public required string Name { get; init; }
    /// <summary>Понятное имя («Обновление Google», «Помощник Adobe»…).</summary>
    public required string DisplayName { get; init; }
    /// <summary>Объяснение простыми словами: что это и почему безопасно закрыть (для «?»-подсказки).</summary>
    public required string Description { get; init; }
    /// <summary>Сколько занимает памяти (суммарный рабочий набор группы).</summary>
    public required long MemoryBytes { get; init; }
}

/// <summary>Состояние раздела «Оптимизация»: память сейчас + список безопасно-закрываемых фоновых процессов.</summary>
public sealed record MemoryOptimizerState
{
    public required MemorySnapshot Memory { get; init; }
    public required IReadOnlyList<OptimizableProcess> Closeable { get; init; }
}
