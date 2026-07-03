using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Снимает состояние наблюдаемых мест системы (папки установки + ветки реестра) для слежения за установкой.</summary>
public interface IInstallSnapshotProbe
{
    /// <summary>Снимает текущий набор файлов/папок и веток реестра в наблюдаемых местах.</summary>
    Task<InstallSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
