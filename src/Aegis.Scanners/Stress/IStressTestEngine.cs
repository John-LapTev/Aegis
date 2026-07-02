namespace Aegis.Scanners.Stress;

/// <summary>
/// Движок проверки под нагрузкой: грузит процессор, следит за температурой, сам останавливается по
/// предохранителям (жара/таймер) и выдаёт вердикт. Отдаёт живой прогресс через <see cref="IProgress{T}"/>.
/// </summary>
public interface IStressTestEngine
{
    /// <summary>Провести проверку выбранного вида. Отмена через <paramref name="cancellationToken"/> = «Стоп».</summary>
    Task<StressTestResult> RunAsync(
        StressTestKind kind,
        IProgress<StressTestProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
