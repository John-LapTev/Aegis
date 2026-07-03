using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Хранит «следы установки» программ, чтобы позже удалить их полностью.</summary>
public interface IInstallTraceStore
{
    /// <summary>Все записанные следы установок.</summary>
    IReadOnlyList<InstallTrace> LoadAll();

    /// <summary>Найти след по имени программы (без учёта регистра), если он записан.</summary>
    InstallTrace? Find(string programName);

    /// <summary>Сохранить (или заменить) след установки.</summary>
    void Save(InstallTrace trace);

    /// <summary>Удалить след по имени программы (после того как всё вычищено).</summary>
    void Remove(string programName);
}
