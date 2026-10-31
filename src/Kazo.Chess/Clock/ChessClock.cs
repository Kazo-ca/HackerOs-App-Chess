using Kazo.Chess.Model;

namespace Kazo.Chess.Clock;

/// <summary>
/// A chess clock, as on a real table: two times, one running, and a button each side presses
/// after its move. It reads the time from a <see cref="TimeProvider"/>, so it measures real elapsed
/// time (a slow render never steals a player's seconds) and tests can move time by hand.
/// </summary>
/// <remarks>
/// The clock starts once White has made its first move (White's opening move is free, as on
/// most online servers). A side's increment is added when it presses after a move it timed.
/// </remarks>
public sealed class ChessClock
{
    private readonly TimeProvider _time;
    private TimeSpan _white;
    private TimeSpan _black;
    private long _runningSince;

    /// <summary>Creates a stopped clock with each side's initial time.</summary>
    /// <param name="control">The time control.</param>
    /// <param name="time">The source of time.</param>
    public ChessClock(TimeControl control, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(time);
        Control = control;
        _time = time;
        _white = control.Initial;
        _black = control.Initial;
    }

    /// <summary>Gets the time control.</summary>
    public TimeControl Control { get; }

    /// <summary>Gets the side whose time runs, or <see langword="null"/> when the clock is stopped.</summary>
    public PieceColor? Running { get; private set; }

    /// <summary>Gets whether the clock was stopped for good (end of the game).</summary>
    public bool IsStopped { get; private set; }

    /// <summary>Returns a side's remaining time, never below zero.</summary>
    /// <param name="side">The side.</param>
    public TimeSpan Remaining(PieceColor side)
    {
        TimeSpan stored = side == PieceColor.White ? _white : _black;
        TimeSpan remaining = Running == side ? stored - _time.GetElapsedTime(_runningSince) : stored;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>Gets the side whose flag fell (no time left), or <see langword="null"/>.</summary>
    public PieceColor? Flagged => Running is { } side && Remaining(side) == TimeSpan.Zero ? side : null;

    /// <summary>
    /// Presses the clock after a side's move: its time stops (plus its increment) and the other
    /// side's starts. White's first press starts the clock.
    /// </summary>
    /// <param name="mover">The side that just moved.</param>
    public void Press(PieceColor mover)
    {
        if (IsStopped)
        {
            return;
        }

        if (Running == mover)
        {
            TimeSpan left = Remaining(mover) + Control.Increment;
            Set(mover, left);
        }

        Running = mover.Opponent();
        _runningSince = _time.GetTimestamp();
    }

    /// <summary>Stops the clock for good: the game is over.</summary>
    public void Stop()
    {
        if (Running is { } side)
        {
            Set(side, Remaining(side));
        }

        Running = null;
        IsStopped = true;
    }

    private void Set(PieceColor side, TimeSpan value)
    {
        if (side == PieceColor.White)
        {
            _white = value;
        }
        else
        {
            _black = value;
        }
    }
}
