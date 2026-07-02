using System.Diagnostics;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник процессов: путь, подпись и мгновенная нагрузка на CPU. Нагрузку меряем сэмплингом —
/// два снимка процессорного времени с коротким интервалом (для эвристики «возможный майнер»). Защищённые
/// системные процессы (путь недоступен) пропускает, чтобы не плодить ложные тревоги.
/// </summary>
public sealed class ProcessProbe : IProcessProbe
{
    /// <summary>Окно измерения нагрузки CPU (мс). Достаточно, чтобы отличить «грузит постоянно» от всплеска.</summary>
    private const int SampleIntervalMilliseconds = 400;

    private readonly record struct Sample(int Pid, string Name, string Path, TimeSpan? CpuTime);

    public async Task<IReadOnlyList<ProcessInfo>> FindAsync(CancellationToken cancellationToken = default)
    {
        // Снимок 1: путь + начальное процессорное время. Дорогую проверку подписи делаем ПОСЛЕ окна
        // измерения, чтобы она не исказила интервал сэмплинга CPU.
        var ownProcessId = Environment.ProcessId; // сам Aegis НЕ показываем как подозрительный процесс
        var samples = new List<Sample>();
        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (process.Id == ownProcessId)
                {
                    continue; // это сам Aegis — себя не сканируем
                }

                var path = TryGetPath(process);
                // Путь недоступен — обычно защищённый системный процесс; не трогаем.
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                samples.Add(new Sample(process.Id, process.ProcessName, path, TryGetCpuTime(process)));
            }
            catch (Exception)
            {
                // Процесс завершился/недоступен — пропускаем.
            }
            finally
            {
                process.Dispose();
            }
        }

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(SampleIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        var processorCount = Environment.ProcessorCount;

        // Снимок 2 (нагрузка) + проверка подписи. Один и тот же .exe (svchost/chrome…) встречается десятки
        // раз — подпись файла за скан не меняется, поэтому проверяем КАЖДЫЙ путь один раз и запоминаем.
        var signatureByPath = new Dictionary<string, (SignatureStatus Signature, string? Publisher)>(StringComparer.OrdinalIgnoreCase);
        var list = new List<ProcessInfo>(samples.Count);
        foreach (var sample in samples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!signatureByPath.TryGetValue(sample.Path, out var signed))
            {
                signed = FileSignatureInspector.Inspect(sample.Path);
                signatureByPath[sample.Path] = signed;
            }

            list.Add(new ProcessInfo
            {
                ProcessId = sample.Pid,
                Name = sample.Name,
                ExecutablePath = sample.Path,
                Signature = signed.Signature,
                Publisher = signed.Publisher,
                CpuPercent = MeasureCpuPercent(sample, elapsedMs, processorCount),
            });
        }

        return list;
    }

    private static double MeasureCpuPercent(Sample sample, double elapsedMs, int processorCount)
    {
        if (sample.CpuTime is null)
        {
            return 0;
        }

        try
        {
            using var process = Process.GetProcessById(sample.Pid);
            var delta = process.TotalProcessorTime - sample.CpuTime.Value;
            return CpuUsage.Percent(delta, elapsedMs, processorCount);
        }
        catch (Exception)
        {
            // Процесс завершился / нет прав на чтение времени — считаем нагрузку неизвестной (0).
            return 0;
        }
    }

    private static TimeSpan? TryGetCpuTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
        }
        catch (Exception)
        {
            // Защищённый процесс — без измерения нагрузки.
            return null;
        }
    }

    private static string TryGetPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
