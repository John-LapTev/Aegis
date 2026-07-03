using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Удаляет установленную программу штатным деинсталлятором и (по флагу) дочищает остатки: пустую папку установки
/// — в Корзину, осиротевшую запись реестра — с бэкапом ветки перед удалением. Реализация Windows-специфична.
/// </summary>
public interface IProgramUninstaller
{
    Task<UninstallResult> UninstallAsync(InstalledProgram program, bool cleanLeftovers, CancellationToken cancellationToken = default);
}
