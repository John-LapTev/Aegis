using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// «Удалить полностью» программу из автозапуска: если есть штатный деинсталлятор — через него + чистка остатков;
/// иначе — папку программы в Корзину (обратимо). Windows-специфично.
/// </summary>
public interface IStartupProgramRemover
{
    /// <summary>Удалить программу по пути её exe и отображаемому имени. Возвращает результат с понятным сообщением.</summary>
    Task<UninstallResult> RemoveAsync(string executablePath, string displayName, CancellationToken cancellationToken = default);
}
