using System.Text.Json.Serialization;
using DesktopClock.Helpers;

namespace DesktopClock.Models;

public sealed class ClockSettings
{
    public double WindowLeft { get; set; } = 120;

    public double WindowTop { get; set; } = 120;

    public double Scale { get; set; } = 1.0;

    public bool IsEditMode { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClockDisplayFormat DisplayFormat { get; set; } = ClockDisplayFormat.HoursMinutes;

    public ClockElementSettings TimeElement { get; set; } = CreateElementDefaults(isVisible: true);

    public ClockElementSettings DateElement { get; set; } = CreateElementDefaults(isVisible: false);

    public ClockElementSettings WeekdayElement { get; set; } = CreateElementDefaults(isVisible: false);

    public bool ShowWeekday { get; set; }

    public bool ShowDate { get; set; }

    public string BackgroundColor { get; set; } = "#0F172A";

    public double BackgroundOpacity { get; set; } = 0.55;

    public double BackgroundWidth { get; set; } = 320;

    public double BackgroundHeight { get; set; } = 120;

    public double CornerRadius { get; set; } = 28;

    public string FontFamilyName { get; set; } = "Segoe UI";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TextFillMode FillMode { get; set; } = TextFillMode.Gradient;

    public List<string> GradientColors { get; set; } = new()
    {
        "#FFFFFF",
        "#CBD5E1"
    };

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GradientDirection GradientDirection { get; set; } = GradientDirection.LeftToRight;

    public bool LaunchAtStartup { get; set; }

    public static ClockSettings CreateDefault() => new();

    public void Normalize()
    {
        if (double.IsNaN(WindowLeft) || double.IsInfinity(WindowLeft))
        {
            WindowLeft = 120;
        }

        if (double.IsNaN(WindowTop) || double.IsInfinity(WindowTop))
        {
            WindowTop = 120;
        }

        Scale = Clamp(Scale, 0.5, 4.0);
        BackgroundOpacity = Clamp(BackgroundOpacity, 0.0, 1.0);
        BackgroundWidth = Clamp(BackgroundWidth, 180, 900);
        BackgroundHeight = Clamp(BackgroundHeight, 72, 360);
        CornerRadius = Clamp(CornerRadius, 0, 120);

        if (string.IsNullOrWhiteSpace(BackgroundColor))
        {
            BackgroundColor = "#0F172A";
        }

        if (string.IsNullOrWhiteSpace(FontFamilyName))
        {
            FontFamilyName = "Segoe UI";
        }

        GradientColors = GradientColors?
            .Where(static color => !string.IsNullOrWhiteSpace(color))
            .Take(2)
            .ToList()
            ?? new List<string>();

        if (GradientColors.Count == 0)
        {
            GradientColors.Add("#FFFFFF");
        }

        if (GradientColors.Count == 1)
        {
            GradientColors.Add(GradientColors[0]);
        }

        TimeElement ??= CreateElementDefaults(isVisible: true);
        DateElement ??= CreateElementDefaults(isVisible: ShowDate);
        WeekdayElement ??= CreateElementDefaults(isVisible: ShowWeekday);

        TimeElement.Normalize(
            defaultVisible: true,
            fallbackFontFamilyName: FontFamilyName,
            fallbackFillMode: FillMode,
            fallbackGradientColors: GradientColors,
            fallbackGradientDirection: GradientDirection);
        TimeElement.CustomFormat = ClockFormatHelpers.NormalizeTimeFormat(TimeElement.CustomFormat, DisplayFormat);
        DisplayFormat = ClockFormatHelpers.InferDisplayFormat(TimeElement.CustomFormat);
        DateElement.Normalize(
            defaultVisible: ShowDate,
            fallbackFontFamilyName: FontFamilyName,
            fallbackFillMode: FillMode,
            fallbackGradientColors: GradientColors,
            fallbackGradientDirection: GradientDirection);
        WeekdayElement.Normalize(
            defaultVisible: ShowWeekday,
            fallbackFontFamilyName: FontFamilyName,
            fallbackFillMode: FillMode,
            fallbackGradientColors: GradientColors,
            fallbackGradientDirection: GradientDirection);

        ShowDate = DateElement.IsVisible;
        ShowWeekday = WeekdayElement.IsVisible;
    }

    public ClockElementSettings GetElementSettings(ClockDisplayItem item)
    {
        return item switch
        {
            ClockDisplayItem.Time => TimeElement,
            ClockDisplayItem.Date => DateElement,
            ClockDisplayItem.Weekday => WeekdayElement,
            _ => TimeElement
        };
    }

    private static ClockElementSettings CreateElementDefaults(bool isVisible)
    {
        return new ClockElementSettings
        {
            IsVisible = isVisible
        };
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return minimum;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}
