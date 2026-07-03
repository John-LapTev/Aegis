using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// «Грубое» удаление файла/папки: если их держит другой процесс — завершает мешающие процессы (кроме критичных
/// системных) и перемещает файл/папку в Корзину (обратимо, не насовсем). Реализация Windows-специфична.
/// </summary>
public interface IForceDeleteService
{
    Task<ForceDeleteResult> DeleteAsync(string path, CancellationToken cancellationToken = default);
}
