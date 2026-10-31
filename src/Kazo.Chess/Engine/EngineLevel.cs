namespace Kazo.Chess.Engine;

/// <summary>The five named strengths of the computer opponent (plan, decision Q31).</summary>
public enum EngineLevel
{
    /// <summary>Beginner: Stockfish "Skill Level" 0.</summary>
    Beginner,

    /// <summary>Easy: "Skill Level" 5.</summary>
    Easy,

    /// <summary>Medium: "Skill Level" 10.</summary>
    Medium,

    /// <summary>Hard: "Skill Level" 15.</summary>
    Hard,

    /// <summary>Expert: "Skill Level" 20, the engine's full strength.</summary>
    Expert,
}

/// <summary>How each <see cref="EngineLevel"/> drives Stockfish.</summary>
public static class EngineLevels
{
    /// <summary>Gets the levels, weakest first.</summary>
    public static IReadOnlyList<EngineLevel> All { get; } = Enum.GetValues<EngineLevel>();

    /// <summary>Returns the UCI <c>Skill Level</c> option of a level: 0, 5, 10, 15 or 20.</summary>
    /// <param name="level">The level.</param>
    public static int SkillLevel(this EngineLevel level) => level switch
    {
        EngineLevel.Beginner => 0,
        EngineLevel.Easy => 5,
        EngineLevel.Medium => 10,
        EngineLevel.Hard => 15,
        EngineLevel.Expert => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    /// <summary>
    /// Returns how long the engine thinks per move, in milliseconds (UCI <c>go movetime</c>). The
    /// stronger levels think longer; even the weakest takes a short pause, as a person would.
    /// </summary>
    /// <param name="level">The level.</param>
    public static int MoveTimeMs(this EngineLevel level) => level switch
    {
        EngineLevel.Beginner => 300,
        EngineLevel.Easy => 400,
        EngineLevel.Medium => 600,
        EngineLevel.Hard => 900,
        EngineLevel.Expert => 1500,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
