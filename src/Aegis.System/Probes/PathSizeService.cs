using Aegis.Core.Abstractions;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный подсчёт занимаемого места по путям. Только читает. Пропускает символические ссылки (иначе одна
/// и та же папка посчиталась бы несколько раз) и недоступные файлы.
/// </summary>
public sealed class PathSizeService : IPathSizeService
{
    public Task<long> MeasureAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return Task.Run(() =>
        {
            long total = 0;

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += Measure(path, cancellationToken);
            }

            return total;
        }, cancellationToken);
    }

    private static long Measure(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }

            if (!Directory.Exists(path))
            {
                return 0; // уже удалено — ровно то, что нужно показать человеку
            }

            long total = 0;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };

            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    total += file.Length;
                }
                catch (Exception)
                {
                    // Файл исчез прямо во время подсчёта — пропускаем.
                }
            }

            return total;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
