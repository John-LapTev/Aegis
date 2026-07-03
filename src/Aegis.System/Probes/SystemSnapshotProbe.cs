using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Снимает текущее состояние системы для функции «Что изменилось»: собирает идентификаторы автозапуска,
/// установленных программ и записей hosts, переиспользуя уже готовые пробники. Только читает.
/// </summary>
public sealed class SystemSnapshotProbe : ISystemSnapshotProbe
{
    private const char Sep = '\u001F';

    private readonly IAutostartProbe _autostart;
    private readonly IInstalledProgramsProbe _programs;
    private readonly INetworkThreatProbe _network;

    public SystemSnapshotProbe(IAutostartProbe autostart, IInstalledProgramsProbe programs, INetworkThreatProbe network)
    {
        ArgumentNullException.ThrowIfNull(autostart);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(network);
        _autostart = autostart;
        _programs = programs;
        _network = network;
    }

    public async Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var autostart = await _autostart.FindAsync(cancellationToken).ConfigureAwait(false);
        var programs = await _programs.FindAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var network = await _network.ReadAsync(cancellationToken).ConfigureAwait(false);

        return new SystemSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            // Идентификатор автозапуска: источник + имя + команда — чтобы отличать «новое», а не переименование.
            Autostart = autostart.Select(e => $"{e.Source}{Sep}{e.Name}{Sep}{e.Command}").Distinct().ToList(),
            Programs = programs.Select(p => p.Name).Distinct().ToList(),
            HostsEntries = network.HostsEntries.Select(h => $"{h.Hostname}{Sep}{h.MappedIp}").Distinct().ToList(),
        };
    }
}
