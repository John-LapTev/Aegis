namespace Aegis.Core.Models;

/// <summary>
/// Накопленная статистика «что Aegis сделал для компьютера» — для раздела «Сравнить состояние» на Дашборде.
/// Копится между запусками. Все счётчики — сумма за всё время использования.
/// </summary>
public sealed record ActivityStats
{
    /// <summary>Сколько всего мусора очищено, байт.</summary>
    public long JunkCleanedBytes { get; init; }

    /// <summary>Сколько раз обновлялись драйверы.</summary>
    public int DriversUpdated { get; init; }

    /// <summary>Сколько программ удалено с очисткой остатков.</summary>
    public int ProgramsRemoved { get; init; }

    /// <summary>Сколько угроз найдено и обезврежено (остановлено/удалено/помещено в карантин).</summary>
    public int ThreatsNeutralized { get; init; }
}
