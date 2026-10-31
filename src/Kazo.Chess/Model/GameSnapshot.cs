namespace Kazo.Chess.Model;

/// <summary>How a game stands: still going, or over and why.</summary>
public enum GameOutcome
{
    /// <summary>The side to move has at least one legal move and no draw rule applies.</summary>
    Ongoing,

    /// <summary>The side to move is in check and has no legal move: the other side wins.</summary>
    Checkmate,

    /// <summary>The side to move is not in check and has no legal move: draw.</summary>
    Stalemate,

    /// <summary>Neither side can mate (for example king against king and bishop): draw.</summary>
    InsufficientMaterial,

    /// <summary>The same position came up three times: draw.</summary>
    ThreefoldRepetition,

    /// <summary>Fifty moves by each side with no capture and no pawn move: draw.</summary>
    FiftyMoveRule,
}

/// <summary>A move the side to move may play, as chess.js lists it.</summary>
/// <param name="From">Square the piece leaves.</param>
/// <param name="To">Square the piece reaches.</param>
/// <param name="San">Standard algebraic notation (<c>Nf3</c>, <c>exd5</c>, <c>O-O</c>, <c>e8=Q+</c>).</param>
/// <param name="Promotion">The piece a pawn becomes, for a promotion; otherwise <see langword="null"/>.</param>
public sealed record LegalMove(Square From, Square To, string San, PieceType? Promotion);

/// <summary>A move already played.</summary>
/// <param name="From">Square the piece left.</param>
/// <param name="To">Square the piece reached.</param>
/// <param name="San">Standard algebraic notation.</param>
/// <param name="Color">The side that played it.</param>
/// <param name="Promotion">The piece a pawn became, for a promotion; otherwise <see langword="null"/>.</param>
public sealed record PlayedMove(Square From, Square To, string San, PieceColor Color, PieceType? Promotion = null)
{
    /// <summary>Gets the move in UCI notation (<c>e2e4</c>, <c>e7e8q</c>), as the engine replays it.</summary>
    public string Uci => new MoveRequest(From, To, Promotion).Uci;
}

/// <summary>
/// Everything the window shows about a position, read from chess.js in one call after each move.
/// Immutable: a move gives a new snapshot. A class, not a record: two snapshots are two moments of a
/// game, compared by reference, and the board read from the FEN belongs to the instance.
/// </summary>
public sealed class GameSnapshot
{
    /// <summary>Creates a snapshot and reads its board from the FEN.</summary>
    /// <param name="fen">The position in Forsyth–Edwards Notation.</param>
    /// <param name="turn">The side to move.</param>
    /// <param name="inCheck">Whether the side to move is in check.</param>
    /// <param name="outcome">Whether the game goes on, or how it ended.</param>
    /// <param name="legalMoves">The moves the side to move may play (empty once the game is over).</param>
    /// <param name="history">The moves played since the starting position, oldest first.</param>
    /// <param name="startFen">The position the game started from; <paramref name="fen"/> when <see langword="null"/>.</param>
    /// <param name="recordedResult">The result a loaded game was saved with (<c>*</c> for a game still going).</param>
    /// <exception cref="FormatException">The FEN placement is not valid.</exception>
    public GameSnapshot(
        string fen,
        PieceColor turn,
        bool inCheck,
        GameOutcome outcome,
        IReadOnlyList<LegalMove> legalMoves,
        IReadOnlyList<PlayedMove> history,
        string? startFen = null,
        string recordedResult = "*")
    {
        ArgumentNullException.ThrowIfNull(legalMoves);
        ArgumentNullException.ThrowIfNull(history);
        Fen = fen;
        StartFen = startFen ?? fen;
        RecordedResult = recordedResult;
        Board = BoardPosition.FromFen(fen);
        Turn = turn;
        InCheck = inCheck;
        Outcome = outcome;
        LegalMoves = legalMoves;
        History = history;
    }

    /// <summary>Gets the position in Forsyth–Edwards Notation.</summary>
    public string Fen { get; }

    /// <summary>Gets the position the game started from (the engine replays <see cref="History"/> from it).</summary>
    public string StartFen { get; }

    /// <summary>
    /// Gets the result a loaded game was saved with: <c>1-0</c>, <c>0-1</c>, <c>1/2-1/2</c>, or
    /// <c>*</c> for a game still going (and for every game played here).
    /// </summary>
    public string RecordedResult { get; }

    /// <summary>Gets the pieces on the board, read from <see cref="Fen"/>.</summary>
    public BoardPosition Board { get; }

    /// <summary>Gets the side to move.</summary>
    public PieceColor Turn { get; }

    /// <summary>Gets whether the side to move is in check.</summary>
    public bool InCheck { get; }

    /// <summary>Gets whether the game goes on, or how it ended.</summary>
    public GameOutcome Outcome { get; }

    /// <summary>Gets the moves the side to move may play (empty once the game is over).</summary>
    public IReadOnlyList<LegalMove> LegalMoves { get; }

    /// <summary>Gets the moves played since the starting position, oldest first.</summary>
    public IReadOnlyList<PlayedMove> History { get; }

    /// <summary>Gets whether the game is over.</summary>
    public bool IsOver => Outcome != GameOutcome.Ongoing;

    /// <summary>Gets the winner after a checkmate (the side that did not have to move), otherwise <see langword="null"/>.</summary>
    public PieceColor? Winner => Outcome == GameOutcome.Checkmate ? Turn.Opponent() : null;

    /// <summary>Gets the last move played, or <see langword="null"/> at the starting position.</summary>
    public PlayedMove? LastMove => History.Count > 0 ? History[^1] : null;

    /// <summary>Returns the legal moves of the piece on a square.</summary>
    /// <param name="from">The square.</param>
    public IEnumerable<LegalMove> MovesFrom(Square from) => LegalMoves.Where(move => move.From == from);
}

/// <summary>Helpers on <see cref="PieceColor"/>.</summary>
public static class PieceColorExtensions
{
    /// <summary>Returns the other side.</summary>
    /// <param name="color">A side.</param>
    public static PieceColor Opponent(this PieceColor color) =>
        color == PieceColor.White ? PieceColor.Black : PieceColor.White;
}
