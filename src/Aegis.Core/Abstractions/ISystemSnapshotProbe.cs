using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Снимает текущее состояние системы (автозапуск, программы, hosts) для функции «Что изменилось». Только читает.</summary>
public interface ISystemSnapshotProbe
{
    Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
