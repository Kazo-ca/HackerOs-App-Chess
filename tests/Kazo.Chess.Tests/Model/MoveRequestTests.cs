using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Model;

public sealed class MoveRequestTests
{
    [Fact]
    public void Uci_WritesFromToAndPromotion()
    {
        Assert.Equal("e2e4", new MoveRequest(Square.Parse("e2"), Square.Parse("e4")).Uci);
        Assert.Equal("e7e8q", new MoveRequest(Square.Parse("e7"), Square.Parse("e8"), PieceType.Queen).Uci);
    }

    [Theory]
    [InlineData("e2e4", "e2", "e4", null)]
    [InlineData("a7a8n", "a7", "a8", PieceType.Knight)]
    public void TryParseUci_ReadsAnEngineMove(string uci, string from, string to, PieceType? promotion)
    {
        Assert.True(MoveRequest.TryParseUci(uci, out MoveRequest? move));
        Assert.Equal(new MoveRequest(Square.Parse(from), Square.Parse(to), promotion), move);
        Assert.Equal(uci, move!.Uci);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("(none)")]
    [InlineData("e2e")]
    [InlineData("e2e9")]
    [InlineData("e7e8k")]
    [InlineData("e7e8p")]
    [InlineData("e7e8x")]
    public void TryParseUci_RefusesWhatIsNotAMove(string? uci) =>
        Assert.False(MoveRequest.TryParseUci(uci, out _));
}
