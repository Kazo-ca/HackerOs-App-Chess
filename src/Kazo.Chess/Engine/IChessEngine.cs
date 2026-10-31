using Kazo.Chess.Model;

namespace Kazo.Chess.Engine;

/// <summary>
/// The computer opponent. The only implementation is <see cref="StockfishEngine"/> (stockfish.js in
/// a Web Worker); tests use a fake.
/// </summary>
public interface IChessEngine : IAsyncDisposable
{
    /// <summary>Tells the engine a new game starts (it forgets what it learnt from the last one).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task NewGameAsync(CancellationToken cancellationToken = default);

    /// <summary>Searches the move to play in a position.</summary>
    /// <param name="fen">The position, side to move included.</param>
    /// <param name="level">How strong the engine plays.</param>
    /// <param name="cancellationToken">Stops the search early; the call then throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>The move, or <see langword="null"/> when the position has none (mate or stalemate).</returns>
    Task<MoveRequest?> BestMoveAsync(string fen, EngineLevel level, CancellationToken cancellationToken = default);
}
