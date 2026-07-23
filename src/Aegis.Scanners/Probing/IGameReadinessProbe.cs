namespace Aegis.Scanners.Probing;

/// <summary>
/// Чтение того, насколько компьютер готов к играм: включено ли аппаратное планирование видеокарты,
/// установлены ли библиотеки, без которых игры не запускаются. Только читает. Реализация Windows-специфична.
/// </summary>
public interface IGameReadinessProbe
{
    Task<GameReadiness> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Состояние «игровой готовности» компьютера.</summary>
public sealed record GameReadiness
{
    /// <summary>Включено ли аппаратное планирование видеокарты (значение <c>HwSchMode</c>: 2 — включено).</summary>
    public bool HardwareSchedulingEnabled { get; init; }

    /// <summary>Есть ли отдельная (не встроенная в процессор) видеокарта.</summary>
    public bool HasDiscreteGpu { get; init; }

    /// <summary>Версия драйверной модели видеокарты ×100 (2700 = WDDM 2.7). 0 — узнать не удалось.</summary>
    public int WddmVersion { get; init; }

    /// <summary>Работает ли Windows на виртуальной машине (там игровые твики бессмысленны).</summary>
    public bool IsVirtualMachine { get; init; }

    /// <summary>Установлен ли пакет Visual C++ (64-разрядный) — без него многие игры не запускаются.</summary>
    public bool HasVisualCppX64 { get; init; }

    /// <summary>Установлен ли пакет Visual C++ (32-разрядный) — нужен старым играм.</summary>
    public bool HasVisualCppX86 { get; init; }

    /// <summary>Установлена ли библиотека DirectX (устаревшая часть, нужна многим играм).</summary>
    public bool HasDirectXRuntime { get; init; }
}
