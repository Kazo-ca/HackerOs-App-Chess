using Kazo.Chess.Interaction;
using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Interaction;

public sealed class BoardInteractionTests
{
    private static readonly GameSnapshot Start = Fixture.Snapshot("start.json");
    private static readonly GameSnapshot Promotion = Fixture.Snapshot("promotion.json");

    private static Square S(string name) => Square.Parse(name);

    [Fact]
    public void Press_OnAMovablePiece_SelectsItAndShowsItsTargets()
    {
        InteractionResult result = BoardInteraction.Initial.Press(Start, PieceColor.White, S("e2"));

        Assert.Equal(S("e2"), result.State.Selected);
        Assert.Null(result.Move);
        Assert.Equal(new HashSet<Square> { S("e3"), S("e4") }, result.State.Targets(Start));
    }

    [Fact]
    public void Press_ThenPressOnATarget_PlaysTheMove()
    {
        BoardInteraction selected = BoardInteraction.Initial.Press(Start, PieceColor.White, S("g1")).State;

        InteractionResult result = selected.Press(Start, PieceColor.White, S("f3"));

        Assert.Equal(new MoveRequest(S("g1"), S("f3")), result.Move);
        Assert.Null(result.State.Selected);
        Assert.Equal(S("f3"), result.State.Cursor);
    }

    [Fact]
    public void Press_OnAnotherOwnPiece_MovesTheSelection()
    {
        BoardInteraction selected = BoardInteraction.Initial.Press(Start, PieceColor.White, S("e2")).State;

        InteractionResult result = selected.Press(Start, PieceColor.White, S("b1"));

        Assert.Equal(S("b1"), result.State.Selected);
        Assert.Null(result.Move);
    }

    [Fact]
    public void Press_OnAnUnreachableSquare_DropsTheSelection()
    {
        BoardInteraction selected = BoardInteraction.Initial.Press(Start, PieceColor.White, S("e2")).State;

        InteractionResult result = selected.Press(Start, PieceColor.White, S("e5"));

        Assert.Null(result.State.Selected);
        Assert.Null(result.Move);
    }

    [Theory]
    [InlineData("e7", PieceColor.White)]
    [InlineData("e2", PieceColor.Black)]
    [InlineData("e2", null)]
    [InlineData("a1", PieceColor.White)]
    public void Press_DoesNotSelectWhatThePlayerMayNotMove(string square, PieceColor? mover)
    {
        InteractionResult result = BoardInteraction.Initial.Press(Start, mover, S(square));

        Assert.Null(result.State.Selected);
    }

    [Fact]
    public void Release_AfterADragOnATarget_PlaysTheMove()
    {
        BoardInteraction pressed = BoardInteraction.Initial.Press(Start, PieceColor.White, S("d2")).State;

        InteractionResult result = pressed.Release(Start, S("d4"), dragged: true);

        Assert.Equal(new MoveRequest(S("d2"), S("d4")), result.Move);
    }

    [Fact]
    public void Release_AfterADragElsewhere_PutsThePieceBackStillSelected()
    {
        BoardInteraction pressed = BoardInteraction.Initial.Press(Start, PieceColor.White, S("d2")).State;

        InteractionResult offTarget = pressed.Release(Start, S("d5"), dragged: true);
        InteractionResult offBoard = pressed.Release(Start, null, dragged: true);

        Assert.Null(offTarget.Move);
        Assert.Equal(S("d2"), offTarget.State.Selected);
        Assert.Null(offBoard.Move);
        Assert.Equal(S("d2"), offBoard.State.Selected);
    }

    [Fact]
    public void Release_WithoutDrag_OnTheReselectedPiece_DeselectsIt()
    {
        BoardInteraction first = BoardInteraction.Initial.Press(Start, PieceColor.White, S("d2")).State;
        first = first.Release(Start, S("d2"), dragged: false).State;
        Assert.Equal(S("d2"), first.Selected);

        BoardInteraction second = first.Press(Start, PieceColor.White, S("d2")).State;
        Assert.Equal(S("d2"), second.Selected);
        InteractionResult released = second.Release(Start, S("d2"), dragged: false);

        Assert.Null(released.State.Selected);
    }

    [Fact]
    public void Release_WithNothingSelected_ChangesNothing()
    {
        InteractionResult result = BoardInteraction.Initial.Release(Start, S("e4"), dragged: true);

        Assert.Same(BoardInteraction.Initial, result.State);
        Assert.Null(result.Move);
    }

    [Fact]
    public void AMoveToTheLastRank_WaitsForThePromotionPiece()
    {
        BoardInteraction selected = BoardInteraction.Initial.Press(Promotion, PieceColor.White, S("a7")).State;

        InteractionResult reached = selected.Press(Promotion, PieceColor.White, S("a8"));

        Assert.Null(reached.Move);
        Assert.Equal(new PendingPromotion(S("a7"), S("a8")), reached.State.Promotion);
        Assert.Same(reached.State, reached.State.Press(Promotion, PieceColor.White, S("a1")).State);

        InteractionResult chosen = reached.State.ChoosePromotion(PieceType.Rook);
        Assert.Equal(new MoveRequest(S("a7"), S("a8"), PieceType.Rook), chosen.Move);
        Assert.Null(chosen.State.Promotion);
    }

    [Theory]
    [InlineData(PieceType.King)]
    [InlineData(PieceType.Pawn)]
    public void ChoosePromotion_RefusesAKingOrAPawn(PieceType piece)
    {
        BoardInteraction pending = BoardInteraction.Initial with { Promotion = new PendingPromotion(S("a7"), S("a8")) };

        InteractionResult result = pending.ChoosePromotion(piece);

        Assert.Null(result.Move);
        Assert.NotNull(result.State.Promotion);
    }

    [Fact]
    public void ChoosePromotion_WithoutAPendingPromotion_ChangesNothing() =>
        Assert.Null(BoardInteraction.Initial.ChoosePromotion(PieceType.Queen).Move);

    [Fact]
    public void Activate_SelectsThenPlaysFromTheKeyboardCursor()
    {
        BoardInteraction onKnight = BoardInteraction.Initial with { Cursor = S("g1") };

        BoardInteraction selected = onKnight.Activate(Start, PieceColor.White).State;
        Assert.Equal(S("g1"), selected.Selected);

        InteractionResult played = (selected with { Cursor = S("h3") }).Activate(Start, PieceColor.White);
        Assert.Equal(new MoveRequest(S("g1"), S("h3")), played.Move);
    }

    [Fact]
    public void Activate_TwiceOnTheSamePiece_DeselectsIt()
    {
        BoardInteraction onPawn = BoardInteraction.Initial with { Cursor = S("c2") };

        BoardInteraction twice = onPawn.Activate(Start, PieceColor.White).State.Activate(Start, PieceColor.White).State;

        Assert.Null(twice.Selected);
    }

    [Theory]
    [InlineData(PieceColor.White, 1, 0, "f2")]
    [InlineData(PieceColor.White, 0, 1, "e3")]
    [InlineData(PieceColor.Black, 1, 0, "d2")]
    [InlineData(PieceColor.Black, 0, 1, "e1")]
    public void MoveCursor_FollowsTheScreenWhateverTheOrientation(PieceColor orientation, int right, int up, string expected)
    {
        BoardInteraction moved = BoardInteraction.Initial.MoveCursor(right, up, orientation);

        Assert.Equal(S(expected), moved.Cursor);
    }

    [Fact]
    public void MoveCursor_StopsAtTheEdge()
    {
        BoardInteraction corner = BoardInteraction.Initial with { Cursor = S("a1") };

        Assert.Equal(S("a1"), corner.MoveCursor(-1, 0, PieceColor.White).Cursor);
        Assert.Equal(S("a1"), corner.MoveCursor(0, -1, PieceColor.White).Cursor);
    }

    [Fact]
    public void Cleared_DropsSelectionAndPromotionButKeepsTheCursor()
    {
        BoardInteraction busy = BoardInteraction.Initial with
        {
            Selected = S("a7"),
            Cursor = S("a8"),
            Promotion = new PendingPromotion(S("a7"), S("a8")),
            DeselectOnRelease = true,
        };

        BoardInteraction cleared = busy.Cleared();

        Assert.Null(cleared.Selected);
        Assert.Null(cleared.Promotion);
        Assert.False(cleared.DeselectOnRelease);
        Assert.Equal(S("a8"), cleared.Cursor);
    }
}
