using System.Diagnostics;

namespace Aegis.System.Internal;

internal enum DefenderResult
{
    Clean,
    ThreatFound,
    Unavailable,
}

/// <summary>
/// Локальная проверка одного файла Защитником Windows через MpCmdRun (офлайн, по локальной базе, без лимитов).
/// Только сканирует и сообщает вердикт (remediation отключён). Недоступен → <see cref="DefenderResult.Unavailable"/>.
/// </summary>
internal static class DefenderScanner
{
    public static async Task<DefenderResult> ScanFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var executable = FindMpCmdRun();
            if (executable is null)
            {
                return DefenderResult.Unavailable;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("-Scan");
            startInfo.ArgumentList.Add("-ScanType");
            startInfo.ArgumentList.Add("3");           // конкретный файл
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-DisableRemediation"); // только проверка, без действий

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return DefenderResult.Unavailable;
            }

            // Сливаем вывод MpCmdRun, иначе он может переполнить буфер канала и проверка зависнет.
            var drainOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var drainErr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(drainOut, drainErr).ConfigureAwait(false);

            // MpCmdRun -Scan: 0 — угроз нет, 2 — найдена угроза.
            return process.ExitCode switch
            {
                0 => DefenderResult.Clean,
                2 => DefenderResult.ThreatFound,
                _ => DefenderResult.Unavailable,
            };
        }
        catch (Exception)
        {
            return DefenderResult.Unavailable;
        }
    }

    private static string? FindMpCmdRun()
    {
        // Актуальная версия лежит в версионированной папке Platform — берём самую свежую.
        try
        {
            var platform = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows Defender", "Platform");
            if (Directory.Exists(platform))
            {
                var latest = Directory.EnumerateDirectories(platform).OrderByDescending(d => d).FirstOrDefault();
                if (latest is not null)
                {
                    var candidate = Path.Combine(latest, "MpCmdRun.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Платформенная папка недоступна — пробуем фиксированный путь ниже.
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
        return File.Exists(fallback) ? fallback : null;
    }
}
