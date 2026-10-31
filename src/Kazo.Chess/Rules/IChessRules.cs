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
}
