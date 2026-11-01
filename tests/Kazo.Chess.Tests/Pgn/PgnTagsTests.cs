using Kazo.Chess.Model;
using Kazo.Chess.Pgn;

namespace Kazo.Chess.Tests.Pgn;

public sealed class PgnTagsTests
{
    [Theory]
    [InlineData("start.json", null, null, "*")]
    [InlineData("fools-mate.json", null, null, "0-1")]
    [InlineData("stalemate.json", null, null, "1/2-1/2")]
    [InlineData("insufficient-material.json", null, null, "1/2-1/2")]
    [InlineData("fifty-moves.json", null, null, "1/2-1/2")]
    [InlineData("start.json", PieceColor.Black, null, "1-0")]
    [InlineData("promotion.json", PieceColor.White, null, "1/2-1/2")]
    [InlineData("start.json", null, "1-0", "1-0")]
    public void ResultOf_WritesThePgnResult(string fixture, PieceColor? timedOut, string? recorded, string result) =>
        Assert.Equal(result, PgnTags.ResultOf(Fixture.Snapshot(fixture), timedOut, recorded));

    [Fact]
    public void FormatDate_UsesDotsAsPgnWants() =>
        Assert.Equal("2026.10.31", PgnTags.FormatDate(new DateOnly(2026, 10, 31)));
}
