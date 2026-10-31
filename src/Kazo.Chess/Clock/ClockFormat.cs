using System.Globalization;

namespace Kazo.Chess.Clock;

/// <summary>How a clock face writes a remaining time, as a real chess clock does.</summary>
public static class ClockFormat
{
    /// <summary>Below this, the face shows tenths of a second: every tenth counts.</summary>
    public static readonly TimeSpan TenthsBelow = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Writes a time: <c>1:05:00</c> from an hour, <c>4:59</c> under an hour, <c>9.4</c> under ten
    /// seconds. Seconds are rounded down, so a face never shows time a player no longer has.
    /// </summary>
    /// <param name="remaining">The remaining time (negative counts as zero).</param>
    public static string Format(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        if (remaining < TenthsBelow)
        {
            long tenths = remaining.Ticks / (TimeSpan.TicksPerSecond / 10);
            return string.Create(CultureInfo.InvariantCulture, $"{tenths / 10}.{tenths % 10}");
        }

        int hours = (int)remaining.TotalHours;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{remaining.Minutes:00}:{remaining.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{remaining.Minutes}:{remaining.Seconds:00}");
    }
}
