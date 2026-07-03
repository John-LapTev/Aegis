using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Хранит накопленную статистику действий Aegis (очищено мусора, обновлено драйверов, удалено программ,
/// обезврежено угроз) между запусками — для раздела «Сравнить состояние».
/// </summary>
public interface IActivityStatsStore
{
    /// <summary>Текущая накопленная статистика.</summary>
    ActivityStats Load();

    /// <summary>Прибавить очищенный мусор (байт).</summary>
    void AddJunkCleaned(long bytes);

    /// <summary>Отметить обновление драйверов.</summary>
    void AddDriversUpdated(int count = 1);

    /// <summary>Отметить удаление программы (с очисткой остатков).</summary>
    void AddProgramsRemoved(int count = 1);

    /// <summary>Отметить обезвреженные угрозы.</summary>
    void AddThreatsNeutralized(int count = 1);
}
