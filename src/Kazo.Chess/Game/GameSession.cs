using System.Runtime.ExceptionServices;
using Kazo.Chess.Clock;
using Kazo.Chess.Engine;
using Kazo.Chess.Model;
using Kazo.Chess.Rules;
using Microsoft.JSInterop;

namespace Kazo.Chess.Game;

/// <summary>
/// One game in the window: the rules, the computer when it plays, and whose turn it is. The window
/// shows <see cref="Snapshot"/> and forwards the player's moves; this class plays the computer's
/// replies and keeps everything in order.
/// </summary>
/// <remarks>
/// Every change of the game goes through one gate, so a move and a new game never interleave. Each
/// new game takes a generation number <em>before</em> waiting for the gate and cancels the computer's
/// search: anything queued behind the gate for an older game (a start, a move, the computer's turn)
/// sees a newer number once inside and gives up, so nothing of an old game reaches a new one.
/// </remarks>
public sealed class GameSession : IAsyncDisposable
{
    private readonly IChessRules _rules;
    private readonly Func<IChessEngine> _engineFactory;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChessEngine? _engine;
    private CancellationTokenSource _search = new();
    private int _generation;

    /// <summary>Creates a session.</summary>
    /// <param name="rules">The rules of the game.</param>
    /// <param name="engineFactory">Creates the computer opponent the first time a game needs it.</param>
    /// <param name="time">The clock's source of time; the system's when <see langword="null"/>.</param>
    public GameSession(IChessRules rules, Func<IChessEngine> engineFactory, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(engineFactory);
        _rules = rules;
        _engineFactory = engineFactory;
        _time = time ?? TimeProvider.System;
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

    /// <summary>Gets whether the computer's engine is being started (its first download takes a moment).</summary>
    public bool IsLoadingEngine { get; private set; }

    /// <summary>Gets the game's clock, or <see langword="null"/> for a game without one.</summary>
    public ChessClock? Clock { get; private set; }

    /// <summary>Gets the side whose time ran out, or <see langword="null"/>.</summary>
    public PieceColor? TimedOut { get; private set; }

    /// <summary>Gets whether the game is over, on the board or on the clock.</summary>
    public bool IsOver => Snapshot?.IsOver == true || TimedOut is not null;

    /// <summary>Gets the side the person at the screen may move now, or <see langword="null"/> when no one may.</summary>
    public PieceColor? Mover => Snapshot is { } snapshot && !IsBusy && !IsThinking && TimedOut is null
        ? Setup.HumanMover(snapshot)
        : null;

    /// <summary>
    /// Gets whether the player may take a move back: not with a clock (it would hand time back),
    /// not while something runs, and against the computer only once the player has moved.
    /// </summary>
    public bool CanUndo =>
        Clock is null
        && Snapshot is { History.Count: > 0 } snapshot
        && !IsBusy
        && !IsThinking
        && (snapshot.IsOver || Mover is not null)
        && (Setup.Mode == GameMode.TwoPlayers || snapshot.History.Any(move => move.Color == Setup.PlayerColor));

    /// <summary>
    /// Starts a new game, abandoning the current one (and the computer's search). When it fails (the
    /// rules or the engine could not start), the current game stays exactly as it was.
    /// </summary>
    /// <param name="setup">How the game is set up.</param>
    /// <param name="cancellationToken">Cancels the call (the window closing).</param>
    public async Task StartAsync(GameSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int generation = Interlocked.Increment(ref _generation);
        await _search.CancelAsync();

        await _gate.WaitAsync(cancellationToken);
        ExceptionDispatchInfo? failure = null;
        try
        {
            if (generation != Volatile.Read(ref _generation))
            {
                // A newer game was asked for while this one waited: it wins.
                return;
            }

            _search.Dispose();
            _search = new CancellationTokenSource();
            SetBusy(true);

            // The engine first, then the rules: the window switches to the new game only once
            // both are ready.
            if (setup.Mode == GameMode.AgainstComputer)
            {
                await StartEngineAsync(cancellationToken);
            }

            GameSnapshot snapshot = await _rules.NewGameAsync(cancellationToken: cancellationToken);
            Snapshot = snapshot;
            Setup = setup;
            Clock = setup.Clock is { } control ? new ChessClock(control, _time) : null;
            TimedOut = null;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            ReleaseGate(busy: false);
        }

        if (failure is not null)
        {
            // The current game goes on: its search was cancelled for nothing, so the computer
            // takes its turn again (when it was its turn) before the failure is reported.
            await ResumeAfterFailedStartAsync(generation, cancellationToken);
            failure.Throw();
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

        int generation = Volatile.Read(ref _generation);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // A new game came in while this move waited, the turn changed, or the player's flag
            // fell before the move: the move belongs to a position that is no longer playable.
            if (generation != Volatile.Read(ref _generation) || CheckTime() || Mover is not { } mover)
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
            PressClock(mover);
        }
        finally
        {
            ReleaseGate(busy: false);
        }

        await ComputerTurnAsync(generation, cancellationToken);
        return true;
    }

    /// <summary>
    /// Takes the player's last move back: one move between two players; against the computer, its
    /// reply too, so it is the player's turn again.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call (the window closing).</param>
    /// <returns><see langword="true"/> when moves were taken back.</returns>
    public async Task<bool> UndoAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUndo)
        {
            return false;
        }

        int generation = Volatile.Read(ref _generation);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (generation != Volatile.Read(ref _generation) || !CanUndo || Snapshot is not { } snapshot)
            {
                return false;
            }

            bool computerReplied = Setup.Mode == GameMode.AgainstComputer
                && snapshot.LastMove?.Color == Setup.ComputerColor
                && snapshot.History.Count >= 2;
            SetBusy(true);
            Snapshot = await _rules.UndoAsync(computerReplied ? 2 : 1, cancellationToken);
        }
        finally
        {
            ReleaseGate(busy: false);
        }

        await ComputerTurnAsync(generation, cancellationToken);
        return true;
    }

    /// <summary>
    /// Looks at the clock: when a side's time is up, the game ends on time (and the computer's
    /// search, if it was thinking, stops). The window calls it on every tick of its display.
    /// </summary>
    /// <returns><see langword="true"/> when the game is over on time.</returns>
    public bool CheckTime()
    {
        if (TimedOut is not null)
        {
            return true;
        }

        if (Clock?.Flagged is not { } flagged)
        {
            return false;
        }

        TimedOut = flagged;
        Clock.Stop();
        _search.Cancel();
        Changed?.Invoke();
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _generation);
        await _search.CancelAsync();
        try
        {
            if (_engine is not null)
            {
                await _engine.DisposeAsync();
            }
        }
        finally
        {
            _engine = null;
            await _rules.DisposeAsync();
        }
    }

    private async Task ResumeAfterFailedStartAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            await ComputerTurnAsync(generation, cancellationToken);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            // The engine went down with the failed start: the start's own failure is the one shown.
        }
    }

    // An engine that cannot start (its worker died, or never answered) is dropped and one fresh
    // engine is tried before giving up, so a dead worker never costs the player two new games.
    private async Task StartEngineAsync(CancellationToken cancellationToken)
    {
        IsLoadingEngine = true;
        Changed?.Invoke();
        try
        {
            await StartEngineOnceOrTwiceAsync(cancellationToken);
        }
        finally
        {
            IsLoadingEngine = false;
        }
    }

    private async Task StartEngineOnceOrTwiceAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            _engine ??= _engineFactory();
            try
            {
                await _engine.NewGameAsync(cancellationToken);
                return;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                await DropEngineAsync();
                if (attempt == 2)
                {
                    throw;
                }
            }
        }
    }

    private async Task DropEngineAsync()
    {
        IChessEngine? engine = _engine;
        _engine = null;
        if (engine is not null)
        {
            try
            {
                await engine.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSException or JSDisconnectedException)
            {
                // A dead worker may not even end cleanly; it is dropped all the same.
            }
        }
    }

    // The computer plays as long as it is its turn in this game. Everything it reads (position,
    // setup, engine, search token) is read under the gate, after checking the game is still this
    // one: a newer game cancels the search and makes the loop give up.
    private async Task ComputerTurnAsync(int generation, CancellationToken cancellationToken)
    {
        while (true)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (generation != Volatile.Read(ref _generation)
                    || CheckTime()
                    || Snapshot is not { IsOver: false } snapshot
                    || snapshot.Turn != Setup.ComputerColor)
                {
                    return;
                }

                IChessEngine engine = _engine
                    ?? throw new InvalidOperationException("The computer has no engine to play with.");
                using var search = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _search.Token);
                SetThinking(true);
                MoveRequest? move;
                try
                {
                    move = await engine.BestMoveAsync(EnginePosition.Of(snapshot), Setup.Level, search.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // A new game cancelled this search.
                    return;
                }
                catch (Exception exception) when (exception is JSException or InvalidOperationException)
                {
                    // The worker died mid-game: the next new game starts a fresh one.
                    await DropEngineAsync();
                    throw;
                }

                if (generation != Volatile.Read(ref _generation) || CheckTime())
                {
                    // A new game came in as the engine answered (the move belongs to the old one),
                    // or the computer's own time ran out while it thought.
                    return;
                }

                MoveRequest found = move
                    ?? throw new InvalidOperationException("The computer found no move in a game that is not over.");
                Snapshot = await _rules.TryMoveAsync(found, cancellationToken)
                    ?? throw new InvalidOperationException($"The rules refused the computer's move {found.Uci}.");
                PressClock(snapshot.Turn);
            }
            finally
            {
                ReleaseGate(thinking: false);
            }
        }
    }

    // After a move: the clock passes to the other side, or stops when the move ended the game.
    private void PressClock(PieceColor mover)
    {
        if (Snapshot is { IsOver: true })
        {
            Clock?.Stop();
        }
        else
        {
            Clock?.Press(mover);
        }
    }

    // The gate is released whatever a Changed subscriber does: a renderer that throws must never
    // lock the game for good.
    private void ReleaseGate(bool? busy = null, bool? thinking = null)
    {
        try
        {
            if (busy is { } isBusy && IsBusy != isBusy)
            {
                SetBusy(isBusy);
            }

            if (thinking is { } isThinking && IsThinking != isThinking)
            {
                SetThinking(isThinking);
            }
        }
        finally
        {
            _gate.Release();
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
