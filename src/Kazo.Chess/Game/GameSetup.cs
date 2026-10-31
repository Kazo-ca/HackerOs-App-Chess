using Kazo.Chess.Engine;
using Kazo.Chess.Model;

namespace Kazo.Chess.Game;

/// <summary>Who plays: two people on the same screen, or one person against the computer.</summary>
public enum GameMode
{
    /// <summary>Two players share the screen; whoever's turn it is may move.</summary>
    TwoPlayers,

    /// <summary>One player against Stockfish.</summary>
    AgainstComputer,
}

/// <summary>The side the player asks for against the computer.</summary>
public enum ColorChoice
{
    /// <summary>The player takes White and moves first.</summary>
    White,

    /// <summary>The player takes Black; the computer opens.</summary>
    Black,

    /// <summary>A coin toss decides.</summary>
    Random,
}

/// <summary>How a game is set up: the mode, and against the computer its level and the player's side.</summary>
/// <param name="Mode">Two players or against the computer.</param>
/// <param name="Level">The computer's level (ignored for two players).</param>
/// <param name="PlayerColor">The player's side against the computer (ignored for two players).</param>
public sealed record GameSetup(GameMode Mode, EngineLevel Level = EngineLevel.Medium, PieceColor PlayerColor = PieceColor.White)
{
    /// <summary>Gets the setup of two players on the same screen.</summary>
    public static GameSetup TwoPlayers { get; } = new(GameMode.TwoPlayers);

    /// <summary>Creates a game against the computer, tossing a coin when the player asked for it.</summary>
    /// <param name="level">The computer's level.</param>
    /// <param name="choice">The side the player asked for.</param>
    /// <param name="random">The coin, for <see cref="ColorChoice.Random"/>.</param>
    public static GameSetup AgainstComputer(EngineLevel level, ColorChoice choice, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        PieceColor color = choice switch
        {
            ColorChoice.White => PieceColor.White,
            ColorChoice.Black => PieceColor.Black,
            _ => random.Next(2) == 0 ? PieceColor.White : PieceColor.Black,
        };
        return new GameSetup(GameMode.AgainstComputer, level, color);
    }

    /// <summary>Gets the computer's side, or <see langword="null"/> between two players.</summary>
    public PieceColor? ComputerColor => Mode == GameMode.AgainstComputer ? PlayerColor.Opponent() : null;

    /// <summary>Gets the side drawn at the bottom of the board: the player's against the computer, White otherwise.</summary>
    public PieceColor Orientation => Mode == GameMode.AgainstComputer ? PlayerColor : PieceColor.White;

    /// <summary>Returns the side a person may move in a position, or <see langword="null"/> when no one may.</summary>
    /// <param name="snapshot">The position.</param>
    public PieceColor? HumanMover(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.IsOver || snapshot.Turn == ComputerColor)
        {
            return null;
        }

        return snapshot.Turn;
    }
}
