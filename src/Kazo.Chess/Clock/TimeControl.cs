using System.Globalization;

namespace Kazo.Chess.Clock;

/// <summary>
/// The time each side has for the whole game and what it gains per move: "5+0" is five minutes,
/// no increment; "15+10" is fifteen minutes plus ten seconds after each move (plan, decision Q32).
/// </summary>
/// <param name="Initial">The time each side starts with.</param>
/// <param name="Increment">The time a side gains after each of its moves.</param>
public sealed record TimeControl(TimeSpan Initial, TimeSpan Increment)
{
    /// <summary>The shortest game a custom control allows, in minutes.</summary>
    public const int MinMinutes = 1;

    /// <summary>The longest game a custom control allows, in minutes.</summary>
    public const int MaxMinutes = 180;

    /// <summary>The largest increment a custom control allows, in seconds.</summary>
    public const int MaxIncrementSeconds = 60;

    /// <summary>Gets the preset controls offered in the new game dialog: 5+0, 10+0 and 15+10.</summary>
    public static IReadOnlyList<TimeControl> Presets { get; } =
    [
        Of(5, 0),
        Of(10, 0),
        Of(15, 10),
    ];

    /// <summary>Gets the control in the usual short form: <c>5+0</c>, <c>15+10</c>.</summary>
    public string Label => string.Create(
        CultureInfo.InvariantCulture,
        $"{(int)Initial.TotalMinutes}+{(int)Increment.TotalSeconds}");

    /// <summary>Creates a control from minutes and seconds of increment, the way players say it.</summary>
    /// <param name="minutes">Minutes per side, <see cref="MinMinutes"/> to <see cref="MaxMinutes"/>.</param>
    /// <param name="incrementSeconds">Seconds gained per move, 0 to <see cref="MaxIncrementSeconds"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside its range.</exception>
    public static TimeControl Of(int minutes, int incrementSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, MinMinutes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutes, MaxMinutes);
        ArgumentOutOfRangeException.ThrowIfNegative(incrementSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(incrementSeconds, MaxIncrementSeconds);
        return new TimeControl(TimeSpan.FromMinutes(minutes), TimeSpan.FromSeconds(incrementSeconds));
    }
}
