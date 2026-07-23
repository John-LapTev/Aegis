using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Avalonia.Controls.ApplicationLifetimes;
using Serilog;

namespace Aegis.App.Services;

/// <summary>
/// Обновление «внутри программы» через релизы GitHub. Проверяет последний релиз публичного репозитория, и если он
/// новее — скачивает новый .exe и заменяет текущий (на Windows работающий .exe можно переименовать, поэтому:
/// текущий → «.old», новый → на его место, запуск новой версии, выход). Остаток «.old» удаляется при следующем старте.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    // Публичный репозиторий проекта — релизы качаются без токена.
    private const string LatestReleaseApi = "https://api.github.com/repos/John-LapTev/Aegis/releases/latest";
    private const string OldSuffix = ".old";

    private static readonly Version CurrentVersion =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Удаляет остатки прошлого обновления («Aegis.exe.old» и незавершённый «Aegis.new.exe») — при старте.</summary>
    public static void CleanupAfterUpdate()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            return;
        }

        var dir = Path.GetDirectoryName(exe);
        var leftovers = new[]
        {
            exe + OldSuffix,
            dir is null ? null : Path.Combine(dir, "Aegis.new.exe"),
        };

        foreach (var path in leftovers)
        {
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                // Файл может быть ещё занят завершающимся старым процессом — не критично, удалим при следующем старте.
                Log.Warning(ex, "Не удалось удалить остаток обновления {Path}", path);
            }
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = CreateClient();
            using var response = await http.GetAsync(LatestReleaseApi, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Раньше любая осечка выглядела как «обновлений нет», и человек был уверен, что версия свежая
                // (жалоба Ивана 1361). Теперь неудача честно отличается от «всё актуально».
                Log.Warning("Проверка обновления: сервер ответил {Status}", (int)response.StatusCode);
                return UpdateCheckResult.Error(DescribeHttpFailure(response.StatusCode));
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (!UpdateVersion.IsNewer(tag, CurrentVersion))
            {
                return UpdateCheckResult.UpToDate; // актуальная версия — обновлять нечего
            }

            var asset = FindExeAsset(root);
            if (asset is null)
            {
                // Релиз есть, но файла программы в нём нет — это не «всё актуально», а именно сбой выпуска.
                Log.Warning("В релизе {Tag} нет файла .exe — обновиться нечем", tag);
                return UpdateCheckResult.Error("В новом выпуске нет файла программы — попробуй позже.");
            }

            var notes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;
            return UpdateCheckResult.Available(new UpdateInfo
            {
                Version = UpdateVersion.Parse(tag)?.ToString() ?? tag!,
                DownloadUrl = asset.Value.Url,
                SizeBytes = asset.Value.Size,
                Sha256Url = FindAssetUrl(root, ".sha256"),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes!.Trim(),
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Проверка обновления не удалась");
            return UpdateCheckResult.Error(DescribeException(ex));
        }
    }

    /// <summary>Почему не удалось проверить — понятными словами (человек не знает, что такое «HTTP 403»).</summary>
    private static string DescribeHttpFailure(global::System.Net.HttpStatusCode status) => status switch
    {
        global::System.Net.HttpStatusCode.Forbidden or (global::System.Net.HttpStatusCode)429 =>
            "GitHub временно ограничил число запросов — попробуй через несколько минут.",
        global::System.Net.HttpStatusCode.NotFound =>
            "Список выпусков не найден. Возможно, обновления пока не публиковались.",
        _ => $"Сервер обновлений ответил ошибкой ({(int)status}). Попробуй позже.",
    };

    /// <summary>Причина сбоя проверки простыми словами (чаще всего — нет интернета).</summary>
    private static string DescribeException(Exception ex) => ex switch
    {
        HttpRequestException => "Не удалось связаться с сервером обновлений — проверь интернет.",
        TaskCanceledException => "Сервер обновлений не ответил вовремя — попробуй ещё раз.",
        _ => "Не удалось проверить обновление: " + ex.Message,
    };

    public async Task<string?> DownloadAndApplyAsync(
        UpdateInfo info, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return "Обновление доступно только для установленной программы (.exe).";
        }

        var dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir))
        {
            return "Не удалось определить папку программы для обновления.";
        }

        var newPath = Path.Combine(dir, "Aegis.new.exe");
        var oldPath = exePath + OldSuffix;
        var swapStarted = false;

        try
        {
            // 1. Скачиваем новый .exe во временный файл рядом (с прогрессом). С проверкой целостности:
            //    битая/оборванная закачка бросит исключение ДО подмены — рабочий .exe останется цел.
            await DownloadFileAsync(info.DownloadUrl, newPath, info.SizeBytes, info.Sha256Url, progress, cancellationToken).ConfigureAwait(false);

            // 2. Меняем местами: работающий .exe переименовываем в «.old», новый ставим на его место.
            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }

            swapStarted = true; // с этого момента откат ВПРАВЕ возвращать «.old»; до него — рабочий .exe трогать нельзя
            File.Move(exePath, oldPath);
            File.Move(newPath, exePath);

            // 3. Запускаем новую версию и закрываем текущую (замена вступит в силу).
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
            });

            Log.Information("Обновление до {Version} установлено, перезапуск", info.Version);

            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось установить обновление");
            TryRollback(exePath, oldPath, newPath, swapStarted);
            return "Не удалось установить обновление: " + ex.Message;
        }
    }

    /// <summary>
    /// Если замена сорвалась — возвращаем РАБОЧИЙ .exe на место. Восстанавливаем из «.old» ТОЛЬКО если подмена реально
    /// началась (<paramref name="swapStarted"/>): тогда рабочая версия точно в «.old» (в т.ч. когда на месте уже лежит
    /// битый новый). Если сбой был ДО подмены (напр. на скачивании) — рабочий .exe не трогаем и НЕ откатываем на старый
    /// уцелевший «.old» (иначе тихий даунгрейд — регресс, аудит 2026-07-04). Временный «.new» убираем всегда.
    /// </summary>
    private static void TryRollback(string exePath, string oldPath, string newPath, bool swapStarted)
    {
        try
        {
            if (File.Exists(newPath))
            {
                File.Delete(newPath); // недокачанный/битый временный файл
            }

            if (swapStarted && File.Exists(oldPath))
            {
                File.Move(oldPath, exePath, overwrite: true); // вернуть рабочую версию поверх (возможно, битого) нового
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Откат неудачного обновления не удался");
        }
    }

    private static async Task DownloadFileAsync(
        string url, string destination, long expectedSize, string? sha256Url,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = expectedSize > 0 ? expectedSize : response.Content.Headers.ContentLength ?? 0L;
        long read = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            int chunk;
            while ((chunk = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken).ConfigureAwait(false);
                read += chunk;
                if (total > 0)
                {
                    progress?.Report((double)read / total);
                }
            }
        }

        // Проверка целостности ДО подмены рабочего .exe: полный ли размер и валидный ли Windows-образ (MZ).
        if (expectedSize > 0 && read != expectedSize)
        {
            throw new IOException($"Обновление скачалось не полностью ({read} из {expectedSize} байт).");
        }

        if (!IsWindowsExecutable(destination))
        {
            throw new IOException("Скачанный файл обновления повреждён (не похож на программу).");
        }

        // Если к релизу приложен ожидаемый SHA-256 — сверяем (доп. защита от подмены/повреждения при передаче).
        if (!string.IsNullOrEmpty(sha256Url))
        {
            await VerifySha256Async(destination, sha256Url, http, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Сверяет SHA-256 скачанного файла с ожидаемым (из «.sha256»-asset релиза). Несовпадение → исключение.</summary>
    private static async Task VerifySha256Async(
        string filePath, string sha256Url, HttpClient http, CancellationToken cancellationToken)
    {
        var expectedRaw = await http.GetStringAsync(sha256Url, cancellationToken).ConfigureAwait(false);
        // Формат «hex  имя_файла» (как у sha256sum) — берём первое слово.
        var parts = expectedRaw.Trim().Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0].Length < 32)
        {
            return; // .sha256 пустой/битый — не блокируем обновление (уже проверили размер+MZ)
        }

        var expected = parts[0];

        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(hashBytes);

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Контрольная сумма обновления не совпала — файл повреждён или подменён.");
        }
    }

    /// <summary>Проверка, что файл начинается с сигнатуры «MZ» — валидный исполняемый файл Windows.</summary>
    private static bool IsWindowsExecutable(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Первый asset релиза с именем, оканчивающимся на «.exe» (ссылка + размер из GitHub API).</summary>
    private static (string Url, long Size)? FindExeAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && asset.TryGetProperty("browser_download_url", out var urlEl)
                && urlEl.GetString() is { Length: > 0 } url)
            {
                var size = asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var s) ? s : 0L;
                return (url, size);
            }
        }

        return null;
    }

    /// <summary>Ссылка на первый asset релиза с именем, оканчивающимся на заданное расширение (напр. «.sha256»); null — нет.</summary>
    private static string? FindAssetUrl(JsonElement release, string extension)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (name is not null && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                && asset.TryGetProperty("browser_download_url", out var urlEl)
                && urlEl.GetString() is { Length: > 0 } url)
            {
                return url;
            }
        }

        return null;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        // GitHub API требует User-Agent, иначе 403.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Aegis-Updater");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
