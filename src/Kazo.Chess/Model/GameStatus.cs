namespace Kazo.Chess.Model;

/// <summary>
/// The line the window shows under the board, as a text key of <c>Strings.resx</c> and the side it
/// names, so the wording stays in the resources and the choice stays testable.
/// </summary>
/// <param name="Key">The text key, for example <c>StatusToMove</c>.</param>
/// <param name="Side">The side named by the text (to move, or winner), or <see langword="null"/>.</param>
public sealed record GameStatus(string Key, PieceColor? Side)
{
    /// <summary>Describes a position.</summary>
    /// <param name="snapshot">The position.</param>
    public static GameStatus Of(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Outcome switch
        {
            GameOutcome.Checkmate => new("StatusCheckmate", snapshot.Winner),
            GameOutcome.Stalemate => new("StatusStalemate", null),
            GameOutcome.InsufficientMaterial => new("StatusInsufficientMaterial", null),
            GameOutcome.ThreefoldRepetition => new("StatusThreefoldRepetition", null),
            GameOutcome.FiftyMoveRule => new("StatusFiftyMoveRule", null),
            _ when snapshot.InCheck => new("StatusCheck", snapshot.Turn),
            _ => new("StatusToMove", snapshot.Turn),
        };
    }
}
