using System.Globalization;
using Kazo.Chess.Model;

namespace Kazo.Chess.Pgn;

/// <summary>
/// The tags a saved game carries (PGN's "seven tag roster"; <c>Round</c> is always <c>?</c>).
/// Values are written as is: PGN escapes nothing but quotes and backslashes, which never appear
/// in what the app writes.
/// </summary>
/// <param name="Event">What the game was: always <c>Casual game</c>.</param>
/// <param name="Site">Where it was played: <c>HackerOS</c>.</param>
/// <param name="Date">The date, as <c>YYYY.MM.DD</c>.</param>
/// <param name="White">The White player.</param>
/// <param name="Black">The Black player.</param>
/// <param name="Result"><c>1-0</c>, <c>0-1</c>, <c>1/2-1/2</c> or <c>*</c> (still going).</param>
public sealed record PgnTags(string Event, string Site, string Date, string White, string Black, string Result)
{
    /// <summary>The PGN tag for a game still going.</summary>
    public const string Ongoing = "*";

    /// <summary>Writes a date as PGN wants it.</summary>
    /// <param name="date">The day the game was played.</param>
    public static string FormatDate(DateOnly date) => date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);

    /// <summary>Returns the PGN result of a position, or of a game lost on time.</summary>
    /// <param name="snapshot">The position.</param>
    /// <param name="timedOut">The side whose time ran out, or <see langword="null"/>.</param>
    /// <param name="recorded">The result a loaded game was saved with, used while its position is not over.</param>
    public static string ResultOf(GameSnapshot snapshot, PieceColor? timedOut = null, string? recorded = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        GameStatus status = GameStatus.Of(snapshot, timedOut);
        return status.Key switch
        {
            "StatusCheckmate" or "StatusTimeOut" => status.Side == PieceColor.White ? "1-0" : "0-1",
            "StatusToMove" or "StatusCheck" => recorded ?? Ongoing,
            _ => "1/2-1/2",
        };
    }
}
