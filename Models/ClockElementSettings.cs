using System.Text.Json.Serialization;

namespace DesktopClock.Models;

public sealed class ClockElementSettings
{
    public bool IsVisible { get; set; } = true;

    public string CustomFormat { get; set; } = string.Empty;

    public string FontFamilyName { get; set; } = "Segoe UI";

    public double SizeScale { get; set; } = 1.0;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TextFillMode FillMode { get; set; } = TextFillMode.Gradient;

    public List<string> GradientColors { get; set; } = new()
    {
        "#FFFFFF",
        "#CBD5E1"
    };

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GradientDirection GradientDirection { get; set; } = GradientDirection.LeftToRight;

    public double OffsetX { get; set; }

    public double OffsetY { get; set; }

    public void Normalize(
        bool defaultVisible,
        string fallbackFontFamilyName,
        TextFillMode fallbackFillMode,
        IReadOnlyList<string>? fallbackGradientColors,
        GradientDirection fallbackGradientDirection)
    {
        if (string.IsNullOrWhiteSpace(FontFamilyName))
        {
            FontFamilyName = string.IsNullOrWhiteSpace(fallbackFontFamilyName) ? "Segoe UI" : fallbackFontFamilyName;
        }

        SizeScale = NormalizeScale(SizeScale);
        CustomFormat = (CustomFormat ?? string.Empty).Trim();

        GradientColors = GradientColors?
            .Where(static color => !string.IsNullOrWhiteSpace(color))
            .Take(2)
            .ToList()
            ?? new List<string>();

        if (GradientColors.Count == 0 && fallbackGradientColors is not null)
        {
            GradientColors = fallbackGradientColors
                .Where(static color => !string.IsNullOrWhiteSpace(color))
                .Take(2)
                .ToList();
        }

        if (GradientColors.Count == 0)
        {
            GradientColors.Add("#FFFFFF");
        }

        if (GradientColors.Count == 1)
        {
            GradientColors.Add(GradientColors[0]);
        }

        FillMode = Enum.IsDefined(typeof(TextFillMode), FillMode) ? FillMode : fallbackFillMode;
        GradientDirection = Enum.IsDefined(typeof(GradientDirection), GradientDirection) ? GradientDirection : fallbackGradientDirection;

        OffsetX = NormalizeOffset(OffsetX);
        OffsetY = NormalizeOffset(OffsetY);
    }

    private static double NormalizeOffset(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Clamp(value, -500, 500);
    }

    private static double NormalizeScale(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1.0;
        }

        return Math.Clamp(value, 0.3, 4.0);
    }
}
