namespace Aegis.Scanners.Stress;

/// <summary>
/// Управляемая нагрузка на процессор для проверки под нагрузкой: <see cref="Start"/> запускает занятые
/// потоки на всех ядрах, <see cref="IDisposable.Dispose"/> их останавливает. За абстракцией — чтобы движок
/// теста тестировался без реальной загрузки процессора.
/// </summary>
public interface ICpuLoad
{
    /// <summary>Начать нагрузку. <paramref name="threadCount"/>=null — по числу ядер. Dispose — остановить.</summary>
    IDisposable Start(int? threadCount = null);
}
