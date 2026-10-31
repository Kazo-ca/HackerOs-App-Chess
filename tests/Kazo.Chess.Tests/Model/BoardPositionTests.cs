using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Model;

public sealed class BoardPositionTests
{
    [Fact]
    public void Start_HasThirtyTwoPiecesInPlace()
    {
        BoardPosition board = BoardPosition.Start;

        Assert.Equal(32, board.Pieces.Count());
        Assert.Equal(new Piece(PieceColor.White, PieceType.King), board[Square.Parse("e1")]);
        Assert.Equal(new Piece(PieceColor.Black, PieceType.Queen), board[Square.Parse("d8")]);
        Assert.Equal(new Piece(PieceColor.White, PieceType.Pawn), board[Square.Parse("a2")]);
        Assert.Null(board[Square.Parse("e4")]);
    }

    [Fact]
    public void FromFen_ReadsOnlyThePlacementOfAFullFen()
    {
        BoardPosition board = BoardPosition.FromFen("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        Assert.Equal(new Piece(PieceColor.White, PieceType.Pawn), board[Square.Parse("e4")]);
        Assert.Null(board[Square.Parse("e2")]);
    }

    [Theory]
    [InlineData("8/8/8/8/8/8/8")]
    [InlineData("9/8/8/8/8/8/8/8")]
    [InlineData("rnbqkbnr/ppppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
    [InlineData("rnbqkbnr/ppppxppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
    [InlineData("rnbqkbnr/ppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
    public void FromFen_RefusesABrokenPlacement(string fen) =>
        Assert.Throws<FormatException>(() => BoardPosition.FromFen(fen));

    [Fact]
    public void FindKing_ReturnsTheKingOfEachSide()
    {
        BoardPosition board = BoardPosition.FromFen("8/P6k/8/8/8/8/8/K7");

        Assert.Equal(Square.Parse("a1"), board.FindKing(PieceColor.White));
        Assert.Equal(Square.Parse("h7"), board.FindKing(PieceColor.Black));
        Assert.Null(BoardPosition.FromFen("8/8/8/8/8/8/8/8").FindKing(PieceColor.White));
    }

    [Theory]
    [InlineData('K', PieceColor.White, PieceType.King, "wK")]
    [InlineData('n', PieceColor.Black, PieceType.Knight, "bN")]
    [InlineData('P', PieceColor.White, PieceType.Pawn, "wP")]
    public void Piece_ReadsFenLettersAndNamesItsImage(char letter, PieceColor color, PieceType type, string image)
    {
        Assert.True(Piece.TryFromFen(letter, out Piece piece));
        Assert.Equal(new Piece(color, type), piece);
        Assert.Equal(image, piece.ImageName);
    }

    [Fact]
    public void Piece_RefusesAnUnknownLetter() => Assert.False(Piece.TryFromFen('x', out _));
}
