using Kazo.Chess.Model;

namespace Kazo.Chess.Interaction;

/// <summary>A promotion waiting for the player to choose the new piece.</summary>
/// <param name="From">Square the pawn leaves.</param>
/// <param name="To">Square on the last rank the pawn reaches.</param>
public sealed record PendingPromotion(Square From, Square To);

/// <summary>What an input changed: the new state and, when the input completed one, a move to play.</summary>
/// <param name="State">The interaction state after the input.</param>
/// <param name="Move">The move to send to the rules, or <see langword="null"/>.</param>
public sealed record InteractionResult(BoardInteraction State, MoveRequest? Move = null);

/// <summary>
/// The state of the player's hand over the board, the same for mouse, touch and keyboard: a selected
/// piece, a keyboard cursor, a promotion waiting for its piece. Immutable: every input returns a new
/// state (and maybe a move), so the board component only renders what it is given.
/// </summary>
/// <remarks>
/// Two ways to move, as on a real board: press the piece then the target square (two taps or two
/// clicks), or drag the piece and drop it on the target. A press on the already selected piece
/// keeps it selected so it can still be dragged; releasing it without a drag deselects it.
/// </remarks>
public sealed record BoardInteraction
{
    /// <summary>Gets the initial state: nothing selected, the cursor on e2.</summary>
    public static BoardInteraction Initial { get; } = new();

    /// <summary>Gets the selected square (a piece the player may move), or <see langword="null"/>.</summary>
    public Square? Selected { get; init; }

    /// <summary>Gets the keyboard cursor.</summary>
    public Square Cursor { get; init; } = new(4, 1);

    /// <summary>Gets the promotion waiting for its piece, or <see langword="null"/>.</summary>
    public PendingPromotion? Promotion { get; init; }

    /// <summary>Gets whether releasing the pointer on the selected square deselects it.</summary>
    public bool DeselectOnRelease { get; init; }

    /// <summary>Returns the squares the selected piece may reach.</summary>
    /// <param name="snapshot">The current position.</param>
    public IReadOnlySet<Square> Targets(GameSnapshot snapshot) =>
        Selected is { } from ? snapshot.MovesFrom(from).Select(move => move.To).ToHashSet() : new HashSet<Square>();

    /// <summary>Handles a press on a square (pointer down, or the first half of a keyboard activation).</summary>
    /// <param name="snapshot">The current position.</param>
    /// <param name="mover">The side the player may move now, or <see langword="null"/> when the board is locked.</param>
    /// <param name="square">The square pressed.</param>
    public InteractionResult Press(GameSnapshot snapshot, PieceColor? mover, Square square)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Promotion is not null)
        {
            return new InteractionResult(this);
        }

        if (Selected is { } from && from != square && Targets(snapshot).Contains(square))
        {
            return Complete(snapshot, from, square);
        }

        if (CanMove(snapshot, mover, square))
        {
            bool reselect = Selected == square;
            return new InteractionResult(this with { Selected = square, Cursor = square, DeselectOnRelease = reselect });
        }

        return new InteractionResult(this with { Selected = null, Cursor = square, DeselectOnRelease = false });
    }

    /// <summary>Handles the release of the pointer after a press.</summary>
    /// <param name="snapshot">The current position.</param>
    /// <param name="square">The square under the pointer when released, or <see langword="null"/> off the board.</param>
    /// <param name="dragged">Whether the pointer moved far enough to be a drag.</param>
    public InteractionResult Release(GameSnapshot snapshot, Square? square, bool dragged)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Promotion is not null || Selected is not { } from)
        {
            return new InteractionResult(this);
        }

        if (!dragged)
        {
            return DeselectOnRelease && square == from
                ? new InteractionResult(this with { Selected = null, DeselectOnRelease = false })
                : new InteractionResult(this with { DeselectOnRelease = false });
        }

        // A drop on a target plays the move; a drop anywhere else puts the piece back, still selected.
        return square is { } to && to != from && Targets(snapshot).Contains(to)
            ? Complete(snapshot, from, to)
            : new InteractionResult(this with { DeselectOnRelease = false });
    }

    /// <summary>Handles Enter or Space on the keyboard cursor: a press and a release without a drag.</summary>
    /// <param name="snapshot">The current position.</param>
    /// <param name="mover">The side the player may move now, or <see langword="null"/> when the board is locked.</param>
    public InteractionResult Activate(GameSnapshot snapshot, PieceColor? mover)
    {
        InteractionResult pressed = Press(snapshot, mover, Cursor);
        return pressed.Move is not null || pressed.State.Promotion is not null
            ? pressed
            : pressed.State.Release(snapshot, Cursor, dragged: false);
    }

    /// <summary>Moves the keyboard cursor by one square in a screen direction.</summary>
    /// <param name="right">+1 for the arrow right, -1 for the arrow left, 0 otherwise.</param>
    /// <param name="up">+1 for the arrow up, -1 for the arrow down, 0 otherwise.</param>
    /// <param name="orientation">The side shown at the bottom of the board.</param>
    public BoardInteraction MoveCursor(int right, int up, PieceColor orientation)
    {
        int sign = orientation == PieceColor.White ? 1 : -1;
        return Cursor.Offset(right * sign, up * sign) is { } next ? this with { Cursor = next } : this;
    }

    /// <summary>Completes a pending promotion with the chosen piece.</summary>
    /// <param name="piece">Queen, rook, bishop or knight.</param>
    public InteractionResult ChoosePromotion(PieceType piece)
    {
        if (Promotion is not { } pending || piece is PieceType.Pawn or PieceType.King)
        {
            return new InteractionResult(this);
        }

        return new InteractionResult(Cleared(), new MoveRequest(pending.From, pending.To, piece));
    }

    /// <summary>Drops the selection and any pending promotion (Escape, or a new position).</summary>
    public BoardInteraction Cleared() => this with { Selected = null, Promotion = null, DeselectOnRelease = false };

    private static bool CanMove(GameSnapshot snapshot, PieceColor? mover, Square square) =>
        mover == snapshot.Turn
        && snapshot.Board[square] is { } piece
        && piece.Color == snapshot.Turn
        && snapshot.MovesFrom(square).Any();

    private InteractionResult Complete(GameSnapshot snapshot, Square from, Square to)
    {
        bool promotes = snapshot.MovesFrom(from).Any(move => move.To == to && move.Promotion is not null);
        return promotes
            ? new InteractionResult(this with { Promotion = new PendingPromotion(from, to), Cursor = to, DeselectOnRelease = false })
            : new InteractionResult(Cleared() with { Cursor = to }, new MoveRequest(from, to));
    }
}
