using Kazo.Chess.Model;

namespace Kazo.Chess.Rules;

/// <summary>
/// One game under the rules of chess. The window holds one per game; the only implementation is
/// <see cref="ChessJsRules"/> (chess.js through JavaScript interop), and tests use a fake.
/// </summary>
public interface IChessRules : IAsyncDisposable
{
    /// <summary>Starts a new game, replacing the current one.</summary>
    /// <param name="fen">Starting position; the standard one when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The starting position.</returns>
    Task<GameSnapshot> NewGameAsync(string? fen = null, CancellationToken cancellationToken = default);

    /// <summary>Plays a move when it is legal.</summary>
    /// <param name="move">The move.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new position, or <see langword="null"/> when the move is illegal (the position does not change).</returns>
    Task<GameSnapshot?> TryMoveAsync(MoveRequest move, CancellationToken cancellationToken = default);

    /// <summary>Starts a game from a saved one (PGN), replacing the current one; its moves are replayed.</summary>
    /// <param name="pgn">The text of a .pgn file.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The position after the last move of the saved game.</returns>
    /// <exception cref="FormatException">The text is not a valid game (the current one is kept).</exception>
    Task<GameSnapshot> LoadPgnAsync(string pgn, CancellationToken cancellationToken = default);

    /// <summary>Writes the current game as PGN.</summary>
    /// <param name="tags">The tags to write before the moves.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text of a .pgn file.</returns>
    Task<string> ExportPgnAsync(Pgn.PgnTags tags, CancellationToken cancellationToken = default);

    /// <summary>Takes moves back.</summary>
    /// <param name="plies">How many half-moves to take back (fewer when the game has fewer).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The position after taking them back.</returns>
    Task<GameSnapshot> UndoAsync(int plies, CancellationToken cancellationToken = default);
}
