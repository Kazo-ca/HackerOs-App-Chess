using Kazo.Chess.Engine;
using Kazo.Chess.Model;
using Kazo.Chess.Rules;

namespace Kazo.Chess.Game;

/// <summary>
/// One game in the window: the rules, the computer when it plays, and whose turn it is. The window
/// shows <see cref="Snapshot"/> and forwards the player's moves; this class plays the computer's
/// replies and keeps everything in order.
/// </summary>
/// <remarks>
/// Every change of the game goes through one gate, so a move and a new game never interleave. A new
/// game first cancels the computer's search (Stockfish stops at once), then takes the gate: the old
/// game's late reply finds a newer generation and is dropped.
/// </remarks>
public sealed class GameSession : IAsyncDisposable
{
    private readonly IChessRules _rules;
    private readonly Func<IChessEngine> _engineFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChessEngine? _engine;
    private CancellationTokenSource _search = new();
    private int _generation;

    /// <summary>Creates a session.</summary>
    /// <param name="rules">The rules of the game.</param>
    /// <param name="engineFactory">Creates the computer opponent the first time a game needs it.</param>
    public GameSession(IChessRules rules, Func<IChessEngine> engineFactory)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(engineFactory);
        _rules = rules;
        _engineFactory = engineFactory;
    }

    /// <summary>Raised after every visible change (position, busy, thinking), for the window to render.</summary>
    public event Action? Changed;

    /// <summary>Gets how the current game is set up.</summary>
    public GameSetup Setup { get; private set; } = GameSetup.TwoPlayers;

    /// <summary>Gets the current position, or <see langword="null"/> before the first game.</summary>
    public GameSnapshot? Snapshot { get; private set; }

    /// <summary>Gets whether a move or a new game is being processed.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>Gets whether the computer is searching its move.</summary>
    public bool IsThinking { get; private set; }

    /// <summary>Gets the side the person at the screen may move now, or <see langword="null"/> when no one may.</summary>
    public PieceColor? Mover => Snapshot is { } snapshot && !IsBusy && !IsThinking ? Setup.HumanMover(snapshot) : null;

    /// <summary>Starts a new game, abandoning the current one (and the computer's search).</summary>
    /// <param name="setup">How the game is set up.</param>
    /// <param name="cancellationToken">Cancels the call (the window closing).</param>
    public async Task StartAsync(GameSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        Interlocked.Increment(ref _generation);
        await _search.CancelAsync();

        await _gate.WaitAsync(cancellationToken);
        int generation = _generation;
        _search.Dispose();
        _search = new CancellationTokenSource();
        try
        {
            SetBusy(true);
            Snapshot = await _rules.NewGameAsync(cancellationToken: cancellationToken);
            Setup = setup;
            if (setup.Mode == GameMode.AgainstComputer)
            {
                await StartEngineAsync(cancellationToken);
            }
        }
        finally
        {
            SetBusy(false);
            _gate.Release();
        }

        await ComputerTurnAsync(generation, cancellationToken);
    }

    /// <summary>Plays the person's move, then the computer's reply when it is its turn.</summary>
    /// <param name="move">The move.</param>
    /// <param name="cancellationToken">Cancels the call (the window closing).</param>
    /// <returns><see langword="true"/> when the move was played; <see langword="false"/> when it was refused or not the player's turn.</returns>
    public async Task<bool> PlayAsync(MoveRequest move, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(move);
        if (Mover is null)
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken);
        int generation = _generation;
        try
        {
            if (Mover is null)
            {
                return false;
            }

            SetBusy(true);
            GameSnapshot? next = await _rules.TryMoveAsync(move, cancellationToken);
            if (next is null)
            {
                return false;
            }

            Snapshot = next;
        }
        finally
        {
            SetBusy(false);
            _gate.Release();
        }

        await ComputerTurnAsync(generation, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _search.CancelAsync();
        if (_engine is not null)
        {
            await _engine.DisposeAsync();
        }

        await _rules.DisposeAsync();
        _search.Dispose();
    }

    // An engine that failed to start (the worker could not load) is dropped, so the next game tries
    // a fresh one instead of reusing a dead worker.
    private async Task StartEngineAsync(CancellationToken cancellationToken)
    {
        _engine ??= _engineFactory();
        try
        {
            await _engine.NewGameAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            IChessEngine failed = _engine;
            _engine = null;
            await failed.DisposeAsync();
            throw;
        }
    }

    // The computer plays as long as it is its turn in this game. A new game (a newer generation)
    // ends the loop: its search is cancelled and its late result is never applied.
    private async Task ComputerTurnAsync(int generation, CancellationToken cancellationToken)
    {
        while (Snapshot is { IsOver: false } snapshot && snapshot.Turn == Setup.ComputerColor && _engine is { } engine)
        {
            using var search = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _search.Token);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (generation != _generation)
                {
                    return;
                }

                SetThinking(true);
                MoveRequest? move = await engine.BestMoveAsync(snapshot.Fen, Setup.Level, search.Token);
                if (move is null || generation != _generation)
                {
                    return;
                }

                Snapshot = await _rules.TryMoveAsync(move, cancellationToken)
                    ?? throw new InvalidOperationException($"The rules refused the computer's move {move.Uci}.");
            }
            catch (OperationCanceledException) when (generation != _generation && !cancellationToken.IsCancellationRequested)
            {
                // A new game cancelled this search.
                return;
            }
            finally
            {
                SetThinking(false);
                _gate.Release();
            }
        }
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        Changed?.Invoke();
    }

    private void SetThinking(bool thinking)
    {
        IsThinking = thinking;
        Changed?.Invoke();
    }
}
