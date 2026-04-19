using System;
using System.Globalization;
using MapChooserSharpMS.Shared.MapConfig;

namespace MapChooserSharpMSEditor.Models;

/// <summary>
/// Editor-side time range. Implements <see cref="ITimeRange"/> so it stays compatible
/// with the MCS schema contract defined in MapChooserSharpMS.Shared.
/// </summary>
public sealed record TimeRangeSpec(TimeOnly StartTime, TimeOnly EndTime) : ITimeRange
{
    public bool IsInRange(TimeOnly time)
    {
        if (StartTime == EndTime)
            return true;
        return StartTime <= EndTime
            ? time >= StartTime && time < EndTime
            : time >= StartTime || time < EndTime;
    }

    public override string ToString() =>
        $"{StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)}-{EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    public static bool TryParse(string? input, out TimeRangeSpec? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var parts = input.Split('-');
        if (parts.Length != 2)
            return false;

        if (!TimeOnly.TryParseExact(parts[0].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            return false;
        if (!TimeOnly.TryParseExact(parts[1].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            return false;

        result = new TimeRangeSpec(start, end);
        return true;
    }

    public static TimeRangeSpec Parse(string input)
    {
        if (!TryParse(input, out var r) || r is null)
            throw new FormatException($"Invalid time range: '{input}'. Expected HH:mm-HH:mm.");
        return r;
    }
}
