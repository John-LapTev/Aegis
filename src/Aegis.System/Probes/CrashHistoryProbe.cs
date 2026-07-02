using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Недавние синие экраны — по файлам-дампам, которые Windows пишет при сбое (BSOD) в папку
/// <c>C:\Windows\Minidump</c>. Считаем дампы за последние 7 дней. Файлы только читаем; ничего не удаляем.
/// Если сбор дампов отключён или папки нет — считаем, что синих экранов не было (0).
/// </summary>
public sealed class CrashHistoryProbe : ICrashHistoryProbe
{
    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    public Task<int> RecentCrashCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var minidumpDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");

            if (!Directory.Exists(minidumpDir))
            {
                return Task.FromResult(0);
            }

            var cutoff = DateTime.UtcNow - Window;
            var count = 0;
            foreach (var dump in Directory.EnumerateFiles(minidumpDir, "*.dmp"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (File.GetLastWriteTimeUtc(dump) >= cutoff)
                    {
                        count++;
                    }
                }
                catch (Exception)
                {
                    // Файл исчез/занят — пропускаем.
                }
            }

            return Task.FromResult(count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(0);
        }
    }
}
