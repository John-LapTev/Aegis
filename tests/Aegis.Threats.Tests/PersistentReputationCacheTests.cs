using Aegis.Core.Models;
using Aegis.Threats.VirusTotal;
using Xunit;

namespace Aegis.Threats.Tests;

public sealed class PersistentReputationCacheTests
{
    [Fact]
    public void Set_ThenReloadFromDisk_RemembersCleanVerdict()
    {
        var path = TempPath();
        try
        {
            var now = DateTimeOffset.UnixEpoch;
            var cache = new PersistentReputationCache(path, TimeSpan.FromDays(30), () => now);
            cache.Set(new FileReputation { Hash = "abc", Verdict = ReputationVerdict.Clean, TotalEngines = 70 });

            // Новый экземпляр читает вердикт с диска — тот же файл (хэш) не требует онлайн-проверки заново.
            var reloaded = new PersistentReputationCache(path, TimeSpan.FromDays(30), () => now);
            var got = reloaded.TryGet("abc");

            Assert.NotNull(got);
            Assert.Equal(ReputationVerdict.Clean, got!.Verdict);
            Assert.Equal(70, got.TotalEngines);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void TryGet_DifferentHash_ReturnsNull()
    {
        // Изменённый файл = другой хэш → записи нет → будет новая проверка (ловим подмену по знакомому пути).
        var path = TempPath();
        try
        {
            var cache = new PersistentReputationCache(path, TimeSpan.FromDays(30), () => DateTimeOffset.UnixEpoch);
            cache.Set(new FileReputation { Hash = "abc", Verdict = ReputationVerdict.Clean });

            Assert.Null(cache.TryGet("a-different-hash"));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void TryGet_StaleEntry_ReturnsNull_ForRecheck()
    {
        // Вердикт старше окна свежести → перепроверить (файл мог со временем попасть в базы как вредонос).
        var path = TempPath();
        try
        {
            var t0 = DateTimeOffset.UnixEpoch;
            var cache = new PersistentReputationCache(path, TimeSpan.FromDays(30), () => t0);
            cache.Set(new FileReputation { Hash = "abc", Verdict = ReputationVerdict.Clean });

            var later = new PersistentReputationCache(path, TimeSpan.FromDays(30), () => t0.AddDays(31));
            Assert.Null(later.TryGet("abc"));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Set_RateLimited_IsNotPersisted()
    {
        var path = TempPath();
        try
        {
            var cache = new PersistentReputationCache(path, now: () => DateTimeOffset.UnixEpoch);
            cache.Set(FileReputation.RateLimited("abc"));

            Assert.Null(cache.TryGet("abc"));
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "aegis-reptest-" + Guid.NewGuid().ToString("N") + ".json");

    private static void Cleanup(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
