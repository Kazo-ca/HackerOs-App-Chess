using Kazo.Chess.Model;
using Kazo.Chess.Rules;

namespace Kazo.Chess.Tests.Rules;

public sealed class GameSnapshotJsonTests
{
    [Fact]
    public void Parse_ReadsTheStartingPosition()
    {
        GameSnapshot snapshot = Fixture.Snapshot("start.json");

        Assert.Equal(PieceColor.White, snapshot.Turn);
        Assert.Equal(GameOutcome.Ongoing, snapshot.Outcome);
        Assert.False(snapshot.InCheck);
        Assert.Equal(20, snapshot.LegalMoves.Count);
        Assert.Empty(snapshot.History);
        Assert.Null(snapshot.LastMove);
        Assert.Equal(32, snapshot.Board.Pieces.Count());
    }

    [Fact]
    public void Parse_ReadsTheHistoryAndTheLastMove()
    {
        GameSnapshot snapshot = Fixture.Snapshot("after-e4.json");

        Assert.Equal(PieceColor.Black, snapshot.Turn);
        Assert.Equal(new PlayedMove(Square.Parse("e2"), Square.Parse("e4"), "e4", PieceColor.White), snapshot.LastMove);
        Assert.Equal(new Piece(PieceColor.White, PieceType.Pawn), snapshot.Board[Square.Parse("e4")]);
    }

    [Fact]
    public void Parse_ReadsACheckmateAndItsWinner()
    {
        GameSnapshot snapshot = Fixture.Snapshot("fools-mate.json");

        Assert.Equal(GameOutcome.Checkmate, snapshot.Outcome);
        Assert.True(snapshot.InCheck);
        Assert.True(snapshot.IsOver);
        Assert.Equal(PieceColor.Black, snapshot.Winner);
        Assert.Empty(snapshot.LegalMoves);
        Assert.Equal("Qh4#", snapshot.LastMove!.San);
    }

    [Fact]
    public void Parse_ReadsOneMovePerPromotionPiece()
    {
        GameSnapshot snapshot = Fixture.Snapshot("promotion.json");

        PieceType?[] promotions = snapshot.MovesFrom(Square.Parse("a7")).Select(move => move.Promotion).Order().ToArray();
        Assert.Equal([PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen], promotions);
    }

    [Theory]
    [InlineData("stalemate.json", GameOutcome.Stalemate)]
    [InlineData("insufficient-material.json", GameOutcome.InsufficientMaterial)]
    [InlineData("fifty-moves.json", GameOutcome.FiftyMoveRule)]
    public void Parse_ReadsTheDraws(string fixture, GameOutcome outcome)
    {
        GameSnapshot snapshot = Fixture.Snapshot(fixture);

        Assert.Equal(outcome, snapshot.Outcome);
        Assert.Null(snapshot.Winner);
    }

    [Fact]
    public void Parse_PrefersCheckmateOverADrawRule()
    {
        string json = Fixture.Json("fools-mate.json").Replace("\"fiftyMoves\": false", "\"fiftyMoves\": true", StringComparison.Ordinal);

        Assert.Equal(GameOutcome.Checkmate, GameSnapshotJson.Parse(json).Outcome);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"turn\":\"w\"}")]
    [InlineData("{\"fen\":\"8/8/8/8/8/8/8/8 w - - 0 1\",\"turn\":\"x\"}")]
    [InlineData("{\"fen\":\"8/8/8/8/8/8/8/8 w - - 0 1\",\"turn\":\"w\",\"moves\":[{\"from\":\"z9\",\"to\":\"a1\"}]}")]
    public void Parse_RefusesWhatIsNotASnapshot(string json) =>
        Assert.Throws<FormatException>(() => GameSnapshotJson.Parse(json));
}
