using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
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

    /// <summary>Удаляет остаток прошлого обновления («Aegis.exe.old») — вызывать при старте приложения.</summary>
    public static void CleanupAfterUpdate()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            var old = exe + OldSuffix;
            if (File.Exists(old))
            {
                File.Delete(old);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось удалить остаток обновления (.old)");
        }
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = CreateClient();
            using var response = await http.GetAsync(LatestReleaseApi, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null; // нет релизов / нет сети / лимит API — молча считаем «обновления нет»
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (!UpdateVersion.IsNewer(tag, CurrentVersion))
            {
                return null; // актуальная версия — обновлять нечего
            }

            var downloadUrl = FindExeAsset(root);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                return null; // у релиза нет .exe — обновлять нечем
            }

            var notes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;
            return new UpdateInfo
            {
                Version = UpdateVersion.Parse(tag)?.ToString() ?? tag!,
                DownloadUrl = downloadUrl,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes!.Trim(),
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Проверка обновления не удалась");
            return null;
        }
    }

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

        try
        {
            // 1. Скачиваем новый .exe во временный файл рядом (с прогрессом).
            await DownloadFileAsync(info.DownloadUrl, newPath, progress, cancellationToken).ConfigureAwait(false);

            // 2. Меняем местами: работающий .exe переименовываем в «.old», новый ставим на его место.
            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }

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
            TryRollback(exePath, oldPath, newPath);
            return "Не удалось установить обновление: " + ex.Message;
        }
    }

    /// <summary>Если замена сорвалась — возвращаем работающий .exe на место и убираем временные файлы.</summary>
    private static void TryRollback(string exePath, string oldPath, string newPath)
    {
        try
        {
            if (!File.Exists(exePath) && File.Exists(oldPath))
            {
                File.Move(oldPath, exePath);
            }

            if (File.Exists(newPath))
            {
                File.Delete(newPath);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Откат неудачного обновления не удался");
        }
    }

    private static async Task DownloadFileAsync(
        string url, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0L;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long read = 0;
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

    /// <summary>Первый asset релиза с именем, оканчивающимся на «.exe».</summary>
    private static string? FindExeAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && asset.TryGetProperty("browser_download_url", out var urlEl))
            {
                return urlEl.GetString();
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
