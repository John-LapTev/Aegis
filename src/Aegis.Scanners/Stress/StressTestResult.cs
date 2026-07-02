using Aegis.Core.Models;

namespace Aegis.Scanners.Stress;

/// <summary>Итог проверки под нагрузкой: пиковые температуры + вердикт простыми словами (попадает в «Здоровье»).</summary>
public sealed record StressTestResult
{
    /// <summary>Какой тест проводили.</summary>
    public required StressTestKind Kind { get; init; }

    /// <summary>Чем закончился (полностью / авто-стоп по жаре / стоп вручную / ошибка).</summary>
    public required StressAbortReason Reason { get; init; }

    /// <summary>Максимальная температура процессора за тест, °C (null — датчик недоступен).</summary>
    public int? MaxCpuCelsius { get; init; }

    /// <summary>Максимальная температура видеокарты за тест, °C (null — датчик недоступен).</summary>
    public int? MaxGpuCelsius { get; init; }

    /// <summary>Сколько секунд реально длился тест.</summary>
    public required int DurationSeconds { get; init; }

    /// <summary>Похоже на тепловой троттлинг (сильный нагрев → компьютер сбрасывал скорость).</summary>
    public bool ThrottlingLikely { get; init; }

    /// <summary>Цвет итога для шкалы 🟢/🟡/🔴.</summary>
    public required Severity Severity { get; init; }

    /// <summary>Вывод простыми словами: всё хорошо / греется / перегрев — что делать.</summary>
    public required string Verdict { get; init; }
}
