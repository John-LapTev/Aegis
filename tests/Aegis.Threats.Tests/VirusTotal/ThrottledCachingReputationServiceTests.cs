using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Threats.VirusTotal;
using Xunit;

namespace Aegis.Threats.Tests.VirusTotal;

/// <summary>
/// Декоратор должен беречь лимит VirusTotal: не дёргать сеть для уже проверенного хэша (кэш) и
/// отдавать RateLimited вместо запроса, когда исчерпан token-bucket (а не молча получать 429).
/// </summary>
public sealed class ThrottledCachingReputationServiceTests
{
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string HashC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public async Task CachesByHash_SecondCheckDoesNotHitInner()
    {
        var inner = new CountingService(ReputationVerdict.Clean);
        var service = new ThrottledCachingReputationService(inner, requestsPerWindow: 10, windowSeconds: 60, nowMs: static () => 0);

        await service.CheckHashAsync(HashA);
        await service.CheckHashAsync(HashA);

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task AfterCapacity_ReturnsRateLimited_WithoutCallingInner()
    {
        long now = 0;
        var inner = new CountingService(ReputationVerdict.Clean);
        var service = new ThrottledCachingReputationService(inner, requestsPerWindow: 2, windowSeconds: 2, nowMs: () => now);

        Assert.Equal(ReputationVerdict.Clean, (await service.CheckHashAsync(HashA)).Verdict);
        Assert.Equal(ReputationVerdict.Clean, (await service.CheckHashAsync(HashB)).Verdict);
        Assert.Equal(ReputationVerdict.RateLimited, (await service.CheckHashAsync(HashC)).Verdict);

        Assert.Equal(2, inner.Calls); // третий запрос до сети не дошёл
    }

    [Fact]
    public async Task RefillsTokensOverTime()
    {
        long now = 0;
        var inner = new CountingService(ReputationVerdict.Clean);
        // 1 токен в секунду.
        var service = new ThrottledCachingReputationService(inner, requestsPerWindow: 1, windowSeconds: 1, nowMs: () => now);

        Assert.Equal(ReputationVerdict.Clean, (await service.CheckHashAsync(HashA)).Verdict);
        Assert.Equal(ReputationVerdict.RateLimited, (await service.CheckHashAsync(HashB)).Verdict);

        now = 1000; // прошла секунда — токен пополнился
        Assert.Equal(ReputationVerdict.Clean, (await service.CheckHashAsync(HashB)).Verdict);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task DoesNotCacheRateLimitedFromInner()
    {
        var inner = new CountingService(ReputationVerdict.RateLimited);
        var service = new ThrottledCachingReputationService(inner, requestsPerWindow: 10, windowSeconds: 60, nowMs: static () => 0);

        await service.CheckHashAsync(HashA);
        await service.CheckHashAsync(HashA);

        // RateLimited не кэшируется → второй раз снова идём к inner (можно повторить позже).
        Assert.Equal(2, inner.Calls);
    }

    private sealed class CountingService(ReputationVerdict verdict) : IThreatReputationService
    {
        public int Calls { get; private set; }

        public Task<FileReputation> CheckHashAsync(string sha256, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new FileReputation { Hash = sha256, Verdict = verdict });
        }
    }
}
