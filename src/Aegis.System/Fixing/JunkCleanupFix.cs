using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Очистка мусора: удаляет файлы в указанных папках (временные/кэш) — в Корзину Windows, а не
/// безвозвратно. Так чистка обратима независимо от того, включена ли в Windows точка восстановления
/// (ADR 0002): любой ошибочно попавший в список файл можно вернуть из Корзины, а место освобождается
/// после её очистки. Занятые/недоступные файлы пропускаются.
/// </summary>
public sealed class JunkCleanupFix : IFix
{
    private readonly IReadOnlyList<string> _paths;
    private readonly bool _permanent;

    public JunkCleanupFix(string findingId, IReadOnlyList<string> paths, bool permanent = false)
    {
        FindingId = findingId;
        _paths = paths;
        _permanent = permanent;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Junk;

    // Удаление мусора/кэша обратимо через Корзину — медленная точка восстановления не нужна.
    public bool RequiresSystemRestorePoint => false;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Сначала СОБИРАЕМ все файлы, потом удаляем ОДНИМ пакетом. Кэш браузера — тысячи мелких файлов;
            // по одному в Корзину это очень медленно, а пакетом (один SHFileOperation) — в разы быстрее.
            var files = new List<string>();
            foreach (var path in _paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Путь — либо ПАПКА (чистим файлы внутри — кэш), либо одиночный ФАЙЛ (cookie/история).
                if (File.Exists(path))
                {
                    files.Add(path);
                    continue;
                }

                if (!Directory.Exists(path))
                {
                    continue;
                }

                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (var file in Directory.EnumerateFiles(path, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(file);
                }
            }

            if (_permanent)
            {
                // Пользователь явно выбрал «навсегда» (с предупреждением) — освобождаем место сразу.
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { File.Delete(file); } catch (Exception) { /* занят/нет прав — пропускаем */ }
                }
            }
            else
            {
                // В Корзину (обратимо) — ОДНИМ пакетом. Занятые/недоступные файлы пропускаются.
                // Если операция целиком не удалась/отменена — честно сообщаем, а не рапортуем «успех».
                if (!RecycleBin.TrySendMany(files))
                {
                    return Task.FromResult(FixOutcome.Failed("Не удалось переместить файлы в Корзину (заняты или отменено)."));
                }
            }

            return Task.FromResult(FixOutcome.OkWithoutBackup());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось очистить: " + ex.Message));
        }
    }
}
