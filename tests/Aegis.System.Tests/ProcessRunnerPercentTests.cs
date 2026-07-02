using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests;

/// <summary>Парсинг живого процента из вывода SFC/DISM (для кольца-прогресса). Чистая логика — проверяется без Windows.</summary>
public sealed class ProcessRunnerPercentTests
{
    [Theory]
    [InlineData("[==========              40.0%                          ]", 0.40)] // DISM progress-bar
    [InlineData("Verification 40% complete.", 0.40)]                                // SFC (English)
    [InlineData("Этап проверки завершён на 40 %", 0.40)]                            // SFC (русский, с пробелом)
    [InlineData("...10.0%...\r...55.5%...", 0.555)]                                 // берём ПОСЛЕДНИЙ процент
    [InlineData("100%", 1.0)]
    [InlineData("0%", 0.0)]
    public void ExtractLastPercent_ParsesKnownFormats(string text, double expected) =>
        Assert.Equal(expected, ProcessRunner.ExtractLastPercent(text)!.Value, 3);

    [Theory]
    [InlineData("no percent here")]
    [InlineData("")]
    [InlineData("текст без процентов")]
    public void ExtractLastPercent_NoPercent_ReturnsNull(string text) =>
        Assert.Null(ProcessRunner.ExtractLastPercent(text));

    [Fact]
    public void ExtractLastPercent_IgnoresOutOfRange() =>
        // «999%» — не процент выполнения; парсер берёт только 0..100.
        Assert.Null(ProcessRunner.ExtractLastPercent("error code 999%"));
}
