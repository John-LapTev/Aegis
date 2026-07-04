using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.App.ViewModels;

/// <summary>
/// Онлайн-проверка репутации файлов при сканировании (Защитник + VirusTotal) — вынесена из MainWindowViewModel
/// как отдельная механика. Неподписанные файлы (процессы/автозапуск) сверяются и, если чисто, помечаются зелёным
/// («без подписи, но безопасно»). Результат кешируется на сессию, чтобы не дёргать сеть/Защитник повторно.
/// </summary>
public sealed class OnlineReputationChecker
{
    private readonly IFileReputationCheck _reputation;
    private readonly ConcurrentDictionary<string, (string Summary, bool Clean)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public OnlineReputationChecker(IFileReputationCheck reputation)
    {
        ArgumentNullException.ThrowIfNull(reputation);
        _reputation = reputation;
    }

    /// <summary>
    /// Проверяет все подходящие находки ПАРАЛЛЕЛЬНО (узкое место — запуск Защитника MpCmdRun на каждый файл).
    /// <paramref name="onProgress"/> — колбэк прогресса (для строки статуса в UI).
    /// </summary>
    public async Task AutoCheckAsync(IEnumerable<FindingViewModel> findings, Action<string> onProgress)
    {
        var targets = findings.Where(f => f.CanCheckOnline && !f.HasOnlineVerdict).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var done = 0;
        using var gate = new SemaphoreSlim(4);
        var tasks = targets.Select(async target =>
        {
            await gate.WaitAsync().ConfigureAwait(true);
            try
            {
                await CheckOneAsync(target).ConfigureAwait(true);
                onProgress($"Проверяю файлы онлайн (Защитник + VirusTotal): {Interlocked.Increment(ref done)} из {targets.Count}…");
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(true);
    }

    private async Task CheckOneAsync(FindingViewModel finding)
    {
        if (finding.IsCheckingOnline)
        {
            return;
        }

        var target = ScanViewHelpers.ExtractExecutablePath(finding.Finding.Detail ?? string.Empty);

        // Уже проверяли этот файл в этой сессии — берём из памяти, не дёргаем сеть/Защитник повторно.
        // ВАЖНО: кеш хранит ЧИСТЫЙ вердикт по ПУТИ; гейт «Danger не красим в зелёное» применяем ПЕР-НАХОДКА
        // и здесь тоже — иначе один и тот же exe (процесс Danger + автозапуск Warning) мог перекрасить
        // опасную находку в зелёное по кешу от безопасной (аудит 2026-07-04).
        if (_cache.TryGetValue(target, out var cached))
        {
            finding.OnlineVerdict = cached.Summary;
            finding.IsVerifiedSafeOnline = cached.Clean && finding.Severity != Severity.Danger;
            return;
        }

        finding.IsCheckingOnline = true;
        finding.OnlineVerdict = "Проверяю онлайн (Защитник + VirusTotal)…";
        try
        {
            var result = await Task.Run(() => _reputation.CheckAsync(target)).ConfigureAwait(true);
            finding.OnlineVerdict = result.Summary;
            var clean = result.Verdict == ReputationVerdict.Clean;
            // Чисто (Защитник + VirusTotal) → файл без подписи, но безопасен → зелёный. НО находку уровня
            // «Проблема» (Danger) по чистому онлайн-вердикту в зелёное НЕ переводим — пусть остаётся на виду.
            finding.IsVerifiedSafeOnline = clean && finding.Severity != Severity.Danger;
            if (!string.IsNullOrEmpty(target))
            {
                _cache[target] = (result.Summary, clean); // в кеш — чистый вердикт, без гейта по severity
            }
        }
        catch (Exception ex)
        {
            finding.OnlineVerdict = "Не удалось проверить: " + ex.Message;
        }
        finally
        {
            finding.IsCheckingOnline = false;
        }
    }
}
