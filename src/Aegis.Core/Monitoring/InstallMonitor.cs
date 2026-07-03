using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Core.Monitoring;

/// <summary>
/// Слежение за установкой (в духе Revo Uninstaller): снимает состояние системы ДО установки, затем ПОСЛЕ,
/// и записывает разницу — всё, что добавил установщик (файлы/папки + ветки реестра). Эта разница («след установки»)
/// потом используется при удалении, чтобы вычистить ВСЁ, что оставила программа. Сравнение — чистая логика, тестируется.
/// </summary>
public sealed class InstallMonitor
{
    private readonly IInstallSnapshotProbe _probe;
    private readonly IInstallTraceStore _store;

    public InstallMonitor(IInstallSnapshotProbe probe, IInstallTraceStore store)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(store);
        _probe = probe;
        _store = store;
    }

    /// <summary>Снять состояние ДО установки (запускать перед запуском установщика).</summary>
    public Task<InstallSnapshot> CaptureBaselineAsync(CancellationToken cancellationToken = default) =>
        _probe.CaptureAsync(cancellationToken);

    /// <summary>Снять состояние ПОСЛЕ установки, вычислить след и сохранить его.</summary>
    public async Task<InstallTrace> RecordAsync(
        string programName, InstallSnapshot baseline, DateTimeOffset capturedAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programName);
        ArgumentNullException.ThrowIfNull(baseline);

        var after = await _probe.CaptureAsync(cancellationToken).ConfigureAwait(false);
        var trace = Diff(programName, baseline, after, capturedAt);
        _store.Save(trace);
        return trace;
    }

    /// <summary>Чистая разница снимков: что появилось ПОСЛЕ, чего не было ДО.</summary>
    public static InstallTrace Diff(string programName, InstallSnapshot before, InstallSnapshot after, DateTimeOffset capturedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programName);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return new InstallTrace
        {
            ProgramName = programName,
            CapturedAt = capturedAt,
            AddedPaths = after.Paths.Except(before.Paths, StringComparer.OrdinalIgnoreCase).ToList(),
            AddedRegistryKeys = after.RegistryKeys.Except(before.RegistryKeys, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }
}
