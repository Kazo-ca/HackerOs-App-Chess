using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Model;

public sealed class GameStatusTests
{
    [Theory]
    [InlineData("start.json", "StatusToMove", PieceColor.White)]
    [InlineData("after-e4.json", "StatusToMove", PieceColor.Black)]
    [InlineData("fools-mate.json", "StatusCheckmate", PieceColor.Black)]
    [InlineData("stalemate.json", "StatusStalemate", null)]
    [InlineData("insufficient-material.json", "StatusInsufficientMaterial", null)]
    [InlineData("fifty-moves.json", "StatusFiftyMoveRule", null)]
    public void Of_NamesTheStatusAndTheSide(string fixture, string key, PieceColor? side)
    {
        GameStatus status = GameStatus.Of(Fixture.Snapshot(fixture));

        Assert.Equal(key, status.Key);
        Assert.Equal(side, status.Side);
    }

    [Fact]
    public void Of_ReportsCheckWhileTheGameGoesOn()
    {
        GameSnapshot snapshot = Rebuild(Fixture.Snapshot("start.json"), inCheck: true);

        Assert.Equal(new GameStatus("StatusCheck", PieceColor.White), GameStatus.Of(snapshot));
    }

    [Fact]
    public void Of_ReportsThreefoldRepetition()
    {
        GameSnapshot snapshot = Rebuild(Fixture.Snapshot("start.json"), outcome: GameOutcome.ThreefoldRepetition);

        Assert.Equal(new GameStatus("StatusThreefoldRepetition", null), GameStatus.Of(snapshot));
    }

    private static GameSnapshot Rebuild(GameSnapshot snapshot, bool? inCheck = null, GameOutcome? outcome = null) =>
        new(snapshot.Fen, snapshot.Turn, inCheck ?? snapshot.InCheck, outcome ?? snapshot.Outcome, snapshot.LegalMoves, snapshot.History);
}
