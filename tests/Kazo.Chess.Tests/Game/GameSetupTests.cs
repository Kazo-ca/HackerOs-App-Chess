using Kazo.Chess.Engine;
using Kazo.Chess.Game;
using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Game;

public sealed class GameSetupTests
{
    private static readonly GameSnapshot Start = Fixture.Snapshot("start.json");
    private static readonly GameSnapshot AfterE4 = Fixture.Snapshot("after-e4.json");

    [Fact]
    public void TwoPlayers_LetWhoeverIsToMovePlay()
    {
        GameSetup setup = GameSetup.TwoPlayers;

        Assert.Null(setup.ComputerColor);
        Assert.Equal(PieceColor.White, setup.Orientation);
        Assert.Equal(PieceColor.White, setup.HumanMover(Start));
        Assert.Equal(PieceColor.Black, setup.HumanMover(AfterE4));
    }

    [Fact]
    public void AgainstComputer_TheBoardFacesThePlayerAndLocksOnTheComputersTurn()
    {
        GameSetup setup = GameSetup.AgainstComputer(EngineLevel.Hard, ColorChoice.Black, new Random(1));

        Assert.Equal(PieceColor.Black, setup.PlayerColor);
        Assert.Equal(PieceColor.White, setup.ComputerColor);
        Assert.Equal(PieceColor.Black, setup.Orientation);
        Assert.Equal(EngineLevel.Hard, setup.Level);
        Assert.Null(setup.HumanMover(Start));
        Assert.Equal(PieceColor.Black, setup.HumanMover(AfterE4));
    }

    [Fact]
    public void NoOneMovesOnceTheGameIsOver() =>
        Assert.Null(GameSetup.TwoPlayers.HumanMover(Fixture.Snapshot("fools-mate.json")));

    [Fact]
    public void Random_GivesBothColours()
    {
        var random = new Random(7);
        HashSet<PieceColor> colors = Enumerable.Range(0, 40)
            .Select(_ => GameSetup.AgainstComputer(EngineLevel.Easy, ColorChoice.Random, random).PlayerColor)
            .ToHashSet();

        Assert.Equal(2, colors.Count);
    }
}
