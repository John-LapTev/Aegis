using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Aegis.System.Internal;

/// <summary>Запуск консольной программы и ожидание завершения. Возвращает код выхода или -1 при ошибке.</summary>
internal static class ProcessRunner
{
    public static async Task<int> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return -1;
            }

            // Отмена долгой операции (DISM/SFC) — реально завершаем процесс, а не просто бросаем ожидание.
            await using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception)
                {
                    // Процесс уже завершился — ничего.
                }
            });

            // Нужен ЖИВОЙ прогресс (DISM/SFC) — читаем stdout кусками и вытаскиваем последний процент.
            if (progress is not null)
            {
                return await RunWithProgressAsync(process, cancellationToken, progress).ConfigureAwait(false);
            }

            // ВАЖНО: подробный вывод (DISM/SFC) надо ВЫЧИТЫВАТЬ, иначе буфер канала переполняется и процесс
            // зависает НАВСЕГДА (ждёт, пока освободят буфер). Сливаем stdout/stderr параллельно с ожиданием.
            var drainOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var drainErr = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                await Task.WhenAll(drainOut, drainErr).ConfigureAwait(false);
                return process.ExitCode;
            }
            finally
            {
                // При отмене WaitForExitAsync бросает до WhenAll — «прибираем» задачи-сливы (иначе UnobservedTaskException).
                try
                {
                    await Task.WhenAll(drainOut, drainErr).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Сливы могли быть отменены/упасть — нам важно лишь «наблюсти» их, не уронить процесс.
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw; // отмена должна дойти до оркестратора, а не превратиться в «ошибку -1»
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>Читает stdout кусками, вытаскивает последний процент и сообщает прогресс (0..1). stderr сливает асинхронно.</summary>
    private static async Task<int> RunWithProgressAsync(Process process, CancellationToken cancellationToken, IProgress<double> progress)
    {
        var drainErr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            var reader = process.StandardOutput;
            var buffer = new char[256];
            var tail = new StringBuilder(700);
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                tail.Append(buffer, 0, read);
                if (tail.Length > 640)
                {
                    tail.Remove(0, tail.Length - 640); // держим только «хвост» — там последний процент
                }

                if (ExtractLastPercent(tail.ToString()) is double fraction)
                {
                    progress.Report(fraction);
                }
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }
        finally
        {
            try
            {
                await drainErr.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // stderr-слив могли отменить — важно лишь «наблюсти» его.
            }
        }
    }

    /// <summary>Последний «NN%»/«NN.N %» из вывода → доля 0..1. Покрывает DISM «[== 40.0% ==]» и SFC «… 40% …»/«… 40 %».</summary>
    internal static double? ExtractLastPercent(string text)
    {
        double? last = null;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '%')
            {
                continue;
            }

            var j = i - 1;
            while (j >= 0 && text[j] == ' ')
            {
                j--;
            }

            var end = j;
            var sawSeparator = false; // у числа максимум один разделитель — иначе «...55.5» съело бы лишние точки
            while (j >= 0)
            {
                if (char.IsDigit(text[j]))
                {
                    j--;
                }
                else if (text[j] is '.' or ',' && !sawSeparator)
                {
                    sawSeparator = true;
                    j--;
                }
                else
                {
                    break;
                }
            }

            if (j + 1 > end)
            {
                continue;
            }

            var token = text[(j + 1)..(end + 1)].Replace(',', '.');
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && value is >= 0 and <= 100)
            {
                last = value / 100.0;
            }
        }

        return last;
    }

    /// <summary>Запуск с захватом стандартного вывода. Возвращает stdout (или пустую строку при ошибке/ненулевом коде).</summary>
    public static async Task<string> RunForOutputAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            // Сливаем оба потока, иначе многословный stderr может переполнить буфер и подвесить процесс.
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var drainErr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(outputTask, drainErr).ConfigureAwait(false);
            return process.ExitCode == 0 ? await outputTask.ConfigureAwait(false) : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>Синхронный запуск с ожиданием завершения. Возвращает true, если код выхода 0.</summary>
    public static bool RunSync(string fileName, string arguments, int timeoutMilliseconds = 15000)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            // Сливаем оба потока (иначе подробный вывод переполнит буфер и процесс зависнет, а операция
            // «тихо» не выполнится — для отката это критично). Пустые обработчики просто опустошают канал.
            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                // Не уложился в таймаут — убиваем, чтобы не оставить зависший reg/schtasks/dism висеть в фоне.
                try { process.Kill(entireProcessTree: true); }
                catch (Exception) { /* мог завершиться сам между проверкой и Kill */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string System(string executable) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), executable);
}
