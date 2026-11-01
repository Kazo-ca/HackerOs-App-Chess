using Kazo.Chess.Model;

namespace Kazo.Chess.Engine;

/// <summary>
/// A position as the engine needs it: where the game started and the moves since, in UCI notation
/// (<c>position fen &lt;start&gt; moves e2e4 e7e5 …</c>). Unlike a lone FEN, it lets Stockfish see
/// repeated positions, so it does not walk into a draw by repetition it could avoid.
/// </summary>
/// <param name="StartFen">The position the game started from.</param>
/// <param name="Moves">The moves played since, oldest first, in UCI notation.</param>
public sealed record EnginePosition(string StartFen, IReadOnlyList<string> Moves)
{
    /// <summary>Returns the engine position of a snapshot.</summary>
    /// <param name="snapshot">The position.</param>
    public static EnginePosition Of(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new EnginePosition(snapshot.StartFen, snapshot.History.Select(move => move.Uci).ToArray());
    }

    /// <summary>Gets the UCI <c>position</c> command.</summary>
    public string Command => Moves.Count == 0
        ? $"position fen {StartFen}"
        : $"position fen {StartFen} moves {string.Join(' ', Moves)}";
}
