using Kazo.Chess.Clock;
using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Clock;

public sealed class ChessClockTests
{
    private static readonly TimeControl FivePlusTwo = TimeControl.Of(5, 2);

    [Fact]
    public void ANewClock_IsStoppedWithTheInitialTime()
    {
        var clock = new ChessClock(FivePlusTwo, new ManualTime());

        Assert.Null(clock.Running);
        Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining(PieceColor.White));
        Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public void WhitesFirstMove_IsFree_AndStartsBlacksTime()
    {
        var time = new ManualTime();
        var clock = new ChessClock(FivePlusTwo, time);
        time.Advance(TimeSpan.FromSeconds(30));

        clock.Press(PieceColor.White);
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(PieceColor.Black, clock.Running);
        Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining(PieceColor.White));
        Assert.Equal(TimeSpan.FromSeconds(290), clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public void Press_AddsTheIncrementToTheTimedMover()
    {
        var time = new ManualTime();
        var clock = new ChessClock(FivePlusTwo, time);
        clock.Press(PieceColor.White);
        time.Advance(TimeSpan.FromSeconds(10));

        clock.Press(PieceColor.Black);

        Assert.Equal(PieceColor.White, clock.Running);
        Assert.Equal(TimeSpan.FromSeconds(292), clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public void AFlagFalls_WhenTheRunningTimeIsUsedUp()
    {
        var time = new ManualTime();
        var clock = new ChessClock(TimeControl.Of(1, 0), time);
        clock.Press(PieceColor.White);

        time.Advance(TimeSpan.FromSeconds(59));
        Assert.Null(clock.Flagged);
        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(PieceColor.Black, clock.Flagged);
        Assert.Equal(TimeSpan.Zero, clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public void Stop_FreezesBothTimesForGood()
    {
        var time = new ManualTime();
        var clock = new ChessClock(FivePlusTwo, time);
        clock.Press(PieceColor.White);
        time.Advance(TimeSpan.FromSeconds(20));

        clock.Stop();
        time.Advance(TimeSpan.FromMinutes(10));
        clock.Press(PieceColor.Black);

        Assert.True(clock.IsStopped);
        Assert.Null(clock.Running);
        Assert.Equal(TimeSpan.FromSeconds(280), clock.Remaining(PieceColor.Black));
        Assert.Null(clock.Flagged);
    }

    [Theory]
    [InlineData(5, 0, "5+0")]
    [InlineData(15, 10, "15+10")]
    public void TimeControl_IsWrittenAsPlayersSayIt(int minutes, int increment, string label) =>
        Assert.Equal(label, TimeControl.Of(minutes, increment).Label);

    [Fact]
    public void TimeControl_PresetsFollowThePlan() =>
        Assert.Equal(["5+0", "10+0", "15+10"], TimeControl.Presets.Select(control => control.Label));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(181, 0)]
    [InlineData(5, -1)]
    [InlineData(5, 61)]
    public void TimeControl_RefusesValuesOutOfRange(int minutes, int increment) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeControl.Of(minutes, increment));

    [Theory]
    [InlineData(3_900_000, "1:05:00")]
    [InlineData(299_999, "4:59")]
    [InlineData(10_000, "0:10")]
    [InlineData(9_470, "9.4")]
    [InlineData(0, "0.0")]
    [InlineData(-500, "0.0")]
    public void ClockFormat_WritesTheFaceOfARealClock(int milliseconds, string face) =>
        Assert.Equal(face, ClockFormat.Format(TimeSpan.FromMilliseconds(milliseconds)));
}
