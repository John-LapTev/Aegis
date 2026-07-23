using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Обновление программы «внутри» (без ручной пересборки): проверка нового релиза на GitHub, скачивание и замена .exe
/// с перезапуском. Реализуется в UI/системном слое.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Проверяет, есть ли релиз новее текущей версии. Итог различает «новой версии нет» и «проверить не
    /// удалось» — молчаливая осечка выглядела как «у вас всё свежее» (жалоба Ивана 1361).
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Скачивает новый .exe и запускает установку (замена файла + перезапуск программы). Возвращает текст ошибки,
    /// либо null, если установка успешно запущена (после чего приложение закрывается и стартует новая версия).
    /// </summary>
    Task<string?> DownloadAndApplyAsync(
        UpdateInfo info, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
