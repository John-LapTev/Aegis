using System;
using Aegis.Core;
using Xunit;

namespace Aegis.Core.Tests;

public sealed class RedistDeletionMatcherTests
{
    private static readonly string[] Installed =
    [
        "Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.34.31931",
        "Microsoft Visual C++ 2015-2022 Redistributable (x86) - 14.30.30704",
        "Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.40664",
        "Microsoft .NET Runtime - 8.0.14 (x64)",
    ];

    [Fact]
    public void SplitDeletions_SeparatesExplanationFromDelLines()
    {
        var answer = "Вот что можно удалить:\n" +
                     "DEL::Microsoft Visual C++ 2015-2022 Redistributable (x86) - 14.30.30704\n" +
                     "Остальное оставить.";

        var (explanation, deletions) = RedistDeletionMatcher.SplitDeletions(answer);

        Assert.Contains("можно удалить", explanation);
        Assert.DoesNotContain("DEL::", explanation);
        Assert.Single(deletions);
        Assert.Equal("Microsoft Visual C++ 2015-2022 Redistributable (x86) - 14.30.30704", deletions[0]);
    }

    [Fact]
    public void SplitDeletions_IgnoresMarkerInMiddleOfProse()
    {
        // «DEL::» не в начале строки (внутри прозы) — не считается командой на удаление.
        var (_, deletions) = RedistDeletionMatcher.SplitDeletions("Не пиши DEL::что-то в тексте объяснения.");

        Assert.Empty(deletions);
    }

    [Fact]
    public void SplitDeletions_StripsListMarkersAndMarkdown()
    {
        var (_, deletions) = RedistDeletionMatcher.SplitDeletions("- DEL::**Microsoft .NET Runtime - 8.0.14 (x64)**");

        Assert.Single(deletions);
        Assert.Equal("Microsoft .NET Runtime - 8.0.14 (x64)", deletions[0]);
    }

    [Fact]
    public void MatchInstalled_ExactName_Matches()
    {
        var match = RedistDeletionMatcher.MatchInstalled(Installed[0], Installed);

        Assert.Equal(Installed[0], match);
    }

    [Fact]
    public void MatchInstalled_X86Request_NeverReturnsX64()
    {
        // Ключевой сценарий бага: ИИ помечает УСТАРЕВШИЙ x86, матчер обязан вернуть именно x86, не x64.
        var aiName = "Microsoft Visual C++ 2015-2022 Redistributable (x86) - 14.30.30704";

        var match = RedistDeletionMatcher.MatchInstalled(aiName, Installed);

        Assert.Equal("Microsoft Visual C++ 2015-2022 Redistributable (x86) - 14.30.30704", match);
        Assert.DoesNotContain("x64", match);
    }

    [Fact]
    public void MatchInstalled_NameWithoutArchitecture_IsAmbiguous_ReturnsNull()
    {
        // Без разрядности нельзя понять, x86 или x64 — fail-safe: не помечаем ничего.
        var match = RedistDeletionMatcher.MatchInstalled(
            "Microsoft Visual C++ 2015-2022 Redistributable", Installed);

        Assert.Null(match);
    }

    [Fact]
    public void MatchInstalled_PrefixWithoutTrailingVersion_MatchesSameArch()
    {
        // ИИ вернул имя без хвоста версии, но с разрядностью — должно однозначно совпасть с x64-записью.
        var match = RedistDeletionMatcher.MatchInstalled(
            "Microsoft Visual C++ 2013 Redistributable (x64)", Installed);

        Assert.Equal("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.40664", match);
    }

    [Fact]
    public void MatchInstalled_UnknownName_ReturnsNull()
    {
        Assert.Null(RedistDeletionMatcher.MatchInstalled("Something Not Installed (x64)", Installed));
    }

    [Fact]
    public void MatchInstalled_EmptyOrNull_ReturnsNull()
    {
        Assert.Null(RedistDeletionMatcher.MatchInstalled("", Installed));
        Assert.Null(RedistDeletionMatcher.MatchInstalled(null, Installed));
        Assert.Null(RedistDeletionMatcher.MatchInstalled("   ", Installed));
    }
}
