using Kazo.Chess.Engine;
using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Engine;

public sealed class EngineTests
{
    [Theory]
    [InlineData(EngineLevel.Beginner, 0)]
    [InlineData(EngineLevel.Easy, 5)]
    [InlineData(EngineLevel.Medium, 10)]
    [InlineData(EngineLevel.Hard, 15)]
    [InlineData(EngineLevel.Expert, 20)]
    public void SkillLevel_FollowsThePlan(EngineLevel level, int skill) => Assert.Equal(skill, level.SkillLevel());

    [Fact]
    public void MoveTime_GrowsWithTheLevel()
    {
        int[] times = EngineLevels.All.Select(level => level.MoveTimeMs()).ToArray();

        Assert.Equal(5, times.Length);
        Assert.Equal(times.Order(), times);
        Assert.All(times, time => Assert.InRange(time, 200, 2000));
    }

    [Theory]
    [InlineData("bestmove e2e4", "e2e4")]
    [InlineData("bestmove e2e4 ponder e7e5", "e2e4")]
    [InlineData("bestmove a7a8q", "a7a8q")]
    public void ParseBestMove_ReadsTheMove(string line, string uci) =>
        Assert.Equal(uci, StockfishEngine.ParseBestMove(line)!.Uci);

    [Fact]
    public void ParseBestMove_ReadsNoMove() => Assert.Null(StockfishEngine.ParseBestMove("bestmove (none)"));

    [Theory]
    [InlineData("")]
    [InlineData("info depth 10")]
    [InlineData("bestmove")]
    [InlineData("bestmove e9e4")]
    public void ParseBestMove_RefusesWhatIsNotABestMove(string line) =>
        Assert.Throws<FormatException>(() => StockfishEngine.ParseBestMove(line));
}
