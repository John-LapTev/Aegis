using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

/// <summary>
/// Регресс на «молчаливо мёртвую» эвристику майнера: раньше нагрузка процессов жёстко равнялась 0,
/// и порог «грузит CPU ≥ 70%» не срабатывал никогда. Проверяем саму арифметику доли загрузки.
/// </summary>
public sealed class CpuUsageTests
{
    [Fact]
    public void Percent_OneCorePeggedForWholeInterval_OnFourCores_Is25()
    {
        // 1000 мс CPU-времени за 1000 мс на 4 ядрах = одно ядро занято = 25% всей мощности.
        var percent = CpuUsage.Percent(TimeSpan.FromMilliseconds(1000), 1000, 4);
        Assert.Equal(25d, percent, precision: 3);
    }

    [Fact]
    public void Percent_AllCoresPegged_Is100()
    {
        // Прирост == интервал × ядра → 100%.
        var percent = CpuUsage.Percent(TimeSpan.FromMilliseconds(4000), 1000, 4);
        Assert.Equal(100d, percent, precision: 3);
    }

    [Fact]
    public void Percent_NoCpuDelta_IsZero() =>
        Assert.Equal(0d, CpuUsage.Percent(TimeSpan.Zero, 1000, 8));

    [Fact]
    public void Percent_ClampsAbove100() =>
        Assert.Equal(100d, CpuUsage.Percent(TimeSpan.FromMilliseconds(99_999), 1000, 4));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Percent_NonPositiveElapsed_IsZero(double elapsedMs) =>
        Assert.Equal(0d, CpuUsage.Percent(TimeSpan.FromMilliseconds(500), elapsedMs, 4));

    [Fact]
    public void Percent_MinerLikeLoad_CrossesSeventyPercentThreshold()
    {
        // Процесс, занявший ~3 ядра из 4 за интервал → ~75% — выше порога «возможный майнер» (70%).
        var percent = CpuUsage.Percent(TimeSpan.FromMilliseconds(3000), 1000, 4);
        Assert.True(percent >= 70d, $"ожидали ≥70%, получили {percent}");
    }
}
