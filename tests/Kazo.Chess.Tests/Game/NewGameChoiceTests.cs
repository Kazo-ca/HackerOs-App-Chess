using Kazo.Chess.Components;
using Kazo.Chess.Engine;
using Kazo.Chess.Game;

namespace Kazo.Chess.Tests.Game;

public sealed class NewGameChoiceTests
{
    [Theory]
    [InlineData(ClockChoice.None, null)]
    [InlineData(ClockChoice.Five, "5+0")]
    [InlineData(ClockChoice.Ten, "10+0")]
    [InlineData(ClockChoice.FifteenPlusTen, "15+10")]
    public void TheClockChoice_GivesItsTimeControl(ClockChoice clock, string? label) =>
        Assert.Equal(label, (NewGameChoice.Default with { Clock = clock }).TimeControl?.Label);

    [Theory]
    [InlineData(3, 2, "3+2")]
    [InlineData(0, 0, "1+0")]
    [InlineData(500, 90, "180+60")]
    public void ACustomClock_IsKeptInRange(int minutes, int increment, string label)
    {
        var choice = new NewGameChoice(GameMode.TwoPlayers, EngineLevel.Medium, ColorChoice.White, ClockChoice.Custom, minutes, increment);

        Assert.Equal(label, choice.TimeControl!.Label);
    }
}
