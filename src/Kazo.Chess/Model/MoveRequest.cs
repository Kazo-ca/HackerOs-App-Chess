namespace Kazo.Chess.Model;

/// <summary>
/// A move a player (or the engine) asks to play. It is only a request: chess.js decides whether it
/// is legal.
/// </summary>
/// <param name="From">Square the piece leaves.</param>
/// <param name="To">Square the piece reaches.</param>
/// <param name="Promotion">The piece a pawn becomes on the last rank; otherwise <see langword="null"/>.</param>
public sealed record MoveRequest(Square From, Square To, PieceType? Promotion = null)
{
    /// <summary>Gets the move in UCI long algebraic notation: <c>e2e4</c>, <c>e7e8q</c>.</summary>
    public string Uci => Promotion is { } piece
        ? $"{From.Name}{To.Name}{PieceLetters.ToLetter(piece)}"
        : $"{From.Name}{To.Name}";

    /// <summary>Reads a move in UCI long algebraic notation, as an engine writes it after <c>bestmove</c>.</summary>
    /// <param name="uci">Four or five characters: <c>e2e4</c>, <c>e7e8q</c>.</param>
    /// <param name="move">The move, when the text is valid.</param>
    /// <returns><see langword="true"/> when <paramref name="uci"/> is a move.</returns>
    public static bool TryParseUci(string? uci, out MoveRequest? move)
    {
        move = null;
        if (uci is not { Length: 4 or 5 }
            || !Square.TryParse(uci[..2], out Square from)
            || !Square.TryParse(uci[2..4], out Square to))
        {
            return false;
        }

        PieceType? promotion = null;
        if (uci.Length == 5)
        {
            if (!PieceLetters.TryFromLetter(uci[4], out PieceType type) || type is PieceType.Pawn or PieceType.King)
            {
                return false;
            }

            promotion = type;
        }

        move = new MoveRequest(from, to, promotion);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Uci;
}
