namespace Kazo.Chess.Model;

/// <summary>
/// The line the window shows under the board, as a text key of <c>Strings.resx</c> and the side it
/// names, so the wording stays in the resources and the choice stays testable.
/// </summary>
/// <param name="Key">The text key, for example <c>StatusToMove</c>.</param>
/// <param name="Side">The side named by the text (to move, or winner), or <see langword="null"/>.</param>
/// <param name="Detail">A value the text quotes (a recorded result), or <see langword="null"/>.</param>
public sealed record GameStatus(string Key, PieceColor? Side, string? Detail = null)
{
    /// <summary>Describes a position, and a fallen flag when the clock ended the game.</summary>
    /// <param name="snapshot">The position.</param>
    /// <param name="timedOut">The side whose time ran out, or <see langword="null"/>.</param>
    /// <param name="recorded">The result a loaded game ended with off the board (resignation,
    /// time), shown while its position is not over; <see langword="null"/> otherwise.</param>
    public static GameStatus Of(GameSnapshot snapshot, PieceColor? timedOut = null, string? recorded = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (timedOut is { } flagged)
        {
            // Out of time, but the opponent has only its king left and could never mate: draw.
            PieceColor opponent = flagged.Opponent();
            bool loneKing = snapshot.Board.Pieces.All(entry => entry.Piece.Color != opponent || entry.Piece.Type == PieceType.King);
            return loneKing ? new("StatusTimeOutDraw", null) : new("StatusTimeOut", opponent);
        }

        return snapshot.Outcome switch
        {
            GameOutcome.Checkmate => new("StatusCheckmate", snapshot.Winner),
            GameOutcome.Stalemate => new("StatusStalemate", null),
            GameOutcome.InsufficientMaterial => new("StatusInsufficientMaterial", null),
            GameOutcome.ThreefoldRepetition => new("StatusThreefoldRepetition", null),
            GameOutcome.FiftyMoveRule => new("StatusFiftyMoveRule", null),
            _ when recorded is not null => new("StatusRecorded", null, recorded),
            _ when snapshot.InCheck => new("StatusCheck", snapshot.Turn),
            _ => new("StatusToMove", snapshot.Turn),
        };
    }
}
