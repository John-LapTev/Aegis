namespace Aegis.Scanners.Probing;

/// <summary>Читает постоянные подписки WMI (CommandLine/ActiveScript event consumers) из root\subscription.</summary>
public interface IWmiPersistenceProbe
{
    Task<IReadOnlyList<WmiPersistence>> FindAsync(CancellationToken cancellationToken = default);
}
