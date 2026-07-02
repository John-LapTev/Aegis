using System.Collections.Concurrent;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.VirusTotal;

/// <summary>
/// Декоратор над <see cref="IThreatReputationService"/>: кэширует вердикты по хэшу (один и тот же файл
/// не дёргает VirusTotal повторно) и ограничивает частоту запросов token-bucket'ом под бесплатный лимит
/// (≈4 запроса/мин). При исчерпании лимита возвращает вердикт <see cref="ReputationVerdict.RateLimited"/>,
/// НЕ обращаясь к сети, — воронка честно сообщит «VirusTotal пропущен из-за лимита, проверено Защитником».
/// Часы (<paramref name="nowMs"/>) инъектируются для детерминированных тестов.
/// </summary>
public sealed class ThrottledCachingReputationService : IThreatReputationService
{
    private readonly IThreatReputationService _inner;
    private readonly Func<long> _nowMs;
    private readonly int _capacity;
    private readonly double _refillIntervalMs;
    private readonly ConcurrentDictionary<string, FileReputation> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly PersistentReputationCache? _persistent;
    private readonly object _gate = new();

    private double _tokens;
    private long _lastRefillMs;

    public ThrottledCachingReputationService(
        IThreatReputationService inner,
        int requestsPerWindow = 4,
        int windowSeconds = 60,
        Func<long>? nowMs = null,
        PersistentReputationCache? persistent = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _capacity = Math.Max(1, requestsPerWindow);
        _refillIntervalMs = Math.Max(1, (double)windowSeconds * 1000 / _capacity); // время на пополнение одного токена
        _nowMs = nowMs ?? (static () => Environment.TickCount64);
        _persistent = persistent;
        _tokens = _capacity;
        _lastRefillMs = _nowMs();
    }

    public async Task<FileReputation> CheckHashAsync(string sha256, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        if (_cache.TryGetValue(sha256, out var cached))
        {
            return cached;
        }

        // Персистентный кэш: тот же файл (тот же хэш), проверенный ранее и ещё свежий — не гоняем сеть снова,
        // даже после перезапуска. Изменённый файл имеет ДРУГОЙ хэш → сюда не попадёт → будет новая проверка.
        if (_persistent?.TryGet(sha256) is { } remembered)
        {
            _cache[sha256] = remembered;
            return remembered;
        }

        if (!TryTakeToken())
        {
            // Лимит исчерпан — в сеть не идём, отдаём «пропущено» и НЕ кэшируем (можно повторить позже).
            return FileReputation.RateLimited(sha256);
        }

        var reputation = await _inner.CheckHashAsync(sha256, cancellationToken).ConfigureAwait(false);

        // Реальные данные кэшируем; «лимит» (в т.ч. 429 от самого VT) — нет.
        if (reputation.Verdict != ReputationVerdict.RateLimited)
        {
            _cache[sha256] = reputation;
            _persistent?.Set(reputation);
        }

        return reputation;
    }

    private bool TryTakeToken()
    {
        lock (_gate)
        {
            var now = _nowMs();
            var elapsed = now - _lastRefillMs;
            if (elapsed > 0)
            {
                // Дробное пополнение копится в _tokens, поэтому остаток времени не теряется.
                _tokens = Math.Min(_capacity, _tokens + elapsed / _refillIntervalMs);
                _lastRefillMs = now;
            }

            if (_tokens >= 1d)
            {
                _tokens -= 1d;
                return true;
            }

            return false;
        }
    }
}
