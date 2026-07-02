namespace Aegis.Scanners.Probing;

/// <summary>Чтение быстрых показателей здоровья (память, время работы, загрузка CPU, вентиляторы). Только читает.</summary>
public interface ISystemVitalsProbe
{
    /// <summary>Снимок показателей прямо сейчас.</summary>
    Task<SystemVitals> ReadAsync(CancellationToken cancellationToken = default);
}
