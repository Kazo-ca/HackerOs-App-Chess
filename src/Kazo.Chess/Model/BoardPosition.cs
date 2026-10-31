namespace Kazo.Chess.Model;

/// <summary>
/// The pieces on the 64 squares, read from the placement field of a FEN string. The board only
/// draws what it is given: every rule (legal moves, check, end of the game) comes from chess.js.
/// </summary>
public sealed class BoardPosition
{
    /// <summary>The placement field of the standard starting position.</summary>
    public const string StartPlacement = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";

    private readonly Piece?[] _squares;

    private BoardPosition(Piece?[] squares) => _squares = squares;

    /// <summary>Gets the standard starting position.</summary>
    public static BoardPosition Start { get; } = FromFen(StartPlacement);

    /// <summary>Returns the piece on a square, or <see langword="null"/> when it is empty.</summary>
    /// <param name="square">The square.</param>
    public Piece? this[Square square] => _squares[Index(square)];

    /// <summary>Returns every occupied square with its piece, a1 first.</summary>
    public IEnumerable<(Square Square, Piece Piece)> Pieces =>
        Square.All.Where(square => this[square] is not null).Select(square => (square, this[square]!.Value));

    /// <summary>Returns the square of a side's king, or <see langword="null"/> when there is none.</summary>
    /// <param name="color">The side.</param>
    public Square? FindKing(PieceColor color)
    {
        foreach ((Square square, Piece piece) in Pieces)
        {
            if (piece == new Piece(color, PieceType.King))
            {
                return square;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the placement field of a FEN string (the text before the first space; the other fields
    /// are ignored). Ranks go from 8 down to 1, separated by <c>/</c>; digits count empty squares.
    /// </summary>
    /// <param name="fen">A full FEN string or only its placement field.</param>
    /// <returns>The position.</returns>
    /// <exception cref="FormatException">The placement field is not valid.</exception>
    public static BoardPosition FromFen(string fen)
    {
        ArgumentNullException.ThrowIfNull(fen);
        string placement = fen.Trim().Split(' ', 2)[0];
        string[] ranks = placement.Split('/');
        if (ranks.Length != Square.Size)
        {
            throw new FormatException($"A FEN placement has 8 ranks, '{placement}' has {ranks.Length}.");
        }

        var squares = new Piece?[Square.Size * Square.Size];
        for (int row = 0; row < Square.Size; row++)
        {
            int rank = Square.Size - 1 - row;
            int file = 0;
            foreach (char letter in ranks[row])
            {
                if (letter is >= '1' and <= '8')
                {
                    file += letter - '0';
                }
                else if (Piece.TryFromFen(letter, out Piece piece) && file < Square.Size)
                {
                    squares[Index(new Square(file, rank))] = piece;
                    file++;
                }
                else
                {
                    throw new FormatException($"Unexpected '{letter}' in rank {rank + 1} of '{placement}'.");
                }
            }

            if (file != Square.Size)
            {
                throw new FormatException($"Rank {rank + 1} of '{placement}' covers {file} squares instead of 8.");
            }
        }

        return new BoardPosition(squares);
    }

    private static int Index(Square square) => (square.Rank * Square.Size) + square.File;
}
