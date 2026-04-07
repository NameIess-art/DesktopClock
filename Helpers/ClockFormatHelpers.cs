using System.Globalization;
using DesktopClock.Models;

namespace DesktopClock.Helpers;

internal static class ClockFormatHelpers
{
    private static readonly DateTime FormatProbe = new(2026, 4, 4, 12, 34, 56);

    internal static string GetFallbackTimeFormat(ClockDisplayFormat displayFormat)
    {
        return displayFormat == ClockDisplayFormat.HoursMinutesSeconds
            ? "HH:mm:ss"
            : "HH:mm";
    }

    internal static string NormalizeTimeFormat(string? customFormat, ClockDisplayFormat displayFormat)
    {
        return string.IsNullOrWhiteSpace(customFormat)
            ? GetFallbackTimeFormat(displayFormat)
            : customFormat.Trim();
    }

    internal static ClockDisplayFormat InferDisplayFormat(string? timeFormat)
    {
        return UsesSecondPrecision(timeFormat)
            ? ClockDisplayFormat.HoursMinutesSeconds
            : ClockDisplayFormat.HoursMinutes;
    }

    internal static bool UsesSecondPrecision(string? timeFormat)
    {
        var format = NormalizeTimeFormat(timeFormat, ClockDisplayFormat.HoursMinutes);

        try
        {
            return !string.Equals(
                FormatProbe.ToString(format, CultureInfo.CurrentCulture),
                FormatProbe.AddSeconds(1).ToString(format, CultureInfo.CurrentCulture),
                StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static string FormatDateTime(DateTime value, string? customFormat, string fallbackFormat, IFormatProvider provider)
    {
        var resolvedFormat = string.IsNullOrWhiteSpace(customFormat) ? fallbackFormat : customFormat.Trim();

        try
        {
            return value.ToString(resolvedFormat, provider);
        }
        catch (FormatException)
        {
            return value.ToString(fallbackFormat, provider);
        }
    }
}
