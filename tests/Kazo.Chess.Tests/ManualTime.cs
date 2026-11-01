namespace Kazo.Chess.Tests;

/// <summary>A clock the test moves by hand: the chess clock reads elapsed time from it.</summary>
internal sealed class ManualTime : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero).AddTicks(_ticks);

    public void Advance(TimeSpan duration) => _ticks += duration.Ticks;
}
