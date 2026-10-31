namespace Kazo.Chess.Model;

/// <summary>The two sides.</summary>
public enum PieceColor
{
    /// <summary>White, moves first.</summary>
    White,

    /// <summary>Black.</summary>
    Black,
}

/// <summary>The six kinds of piece.</summary>
public enum PieceType
{
    /// <summary>Pawn (<c>p</c>).</summary>
    Pawn,

    /// <summary>Knight (<c>n</c>).</summary>
    Knight,

    /// <summary>Bishop (<c>b</c>).</summary>
    Bishop,

    /// <summary>Rook (<c>r</c>).</summary>
    Rook,

    /// <summary>Queen (<c>q</c>).</summary>
    Queen,

    /// <summary>King (<c>k</c>).</summary>
    King,
}

/// <summary>A piece on the board: its side and its kind.</summary>
/// <param name="Color">The side the piece belongs to.</param>
/// <param name="Type">The kind of piece.</param>
public readonly record struct Piece(PieceColor Color, PieceType Type)
{
    /// <summary>
    /// Gets the file name of the piece's image, as the piece sets name them: <c>w</c> or <c>b</c>,
    /// then the upper-case letter (<c>wK</c>, <c>bN</c>).
    /// </summary>
    public string ImageName => $"{(Color == PieceColor.White ? 'w' : 'b')}{char.ToUpperInvariant(PieceLetters.ToLetter(Type))}";

    /// <summary>Reads a FEN piece letter: upper case for White, lower case for Black.</summary>
    /// <param name="letter">One of <c>PNBRQK</c> or <c>pnbrqk</c>.</param>
    /// <param name="piece">The piece, when the letter is valid.</param>
    /// <returns><see langword="true"/> when <paramref name="letter"/> names a piece.</returns>
    public static bool TryFromFen(char letter, out Piece piece)
    {
        piece = default;
        if (!PieceLetters.TryFromLetter(char.ToLowerInvariant(letter), out PieceType type))
        {
            return false;
        }

        piece = new Piece(char.IsUpper(letter) ? PieceColor.White : PieceColor.Black, type);
        return true;
    }
}

/// <summary>The one-letter names of the pieces, as chess.js, FEN and UCI write them (lower case).</summary>
public static class PieceLetters
{
    /// <summary>Returns the lower-case letter of a kind of piece.</summary>
    /// <param name="type">The kind of piece.</param>
    public static char ToLetter(PieceType type) => type switch
    {
        PieceType.Pawn => 'p',
        PieceType.Knight => 'n',
        PieceType.Bishop => 'b',
        PieceType.Rook => 'r',
        PieceType.Queen => 'q',
        PieceType.King => 'k',
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>Reads a lower-case piece letter.</summary>
    /// <param name="letter">One of <c>pnbrqk</c>.</param>
    /// <param name="type">The kind of piece, when the letter is valid.</param>
    /// <returns><see langword="true"/> when <paramref name="letter"/> names a piece.</returns>
    public static bool TryFromLetter(char letter, out PieceType type)
    {
        (bool known, type) = letter switch
        {
            'p' => (true, PieceType.Pawn),
            'n' => (true, PieceType.Knight),
            'b' => (true, PieceType.Bishop),
            'r' => (true, PieceType.Rook),
            'q' => (true, PieceType.Queen),
            'k' => (true, PieceType.King),
            _ => (false, default),
        };
        return known;
    }
}
