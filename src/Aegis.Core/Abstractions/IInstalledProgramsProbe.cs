using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>Читает список установленных программ (ветки «Uninstall» реестра) для раздела «Удаление программ». Только читает.</summary>
public interface IInstalledProgramsProbe
{
    /// <param name="includeHidden">Включать ли системные/скрытые программы и обновления Windows (по умолчанию — нет).</param>
    Task<IReadOnlyList<InstalledProgram>> FindAsync(bool includeHidden = false, CancellationToken cancellationToken = default);
}
