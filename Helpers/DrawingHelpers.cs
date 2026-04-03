using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopClock.Helpers;

internal static class DrawingHelpers
{
    internal static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch
        {
            return fallback;
        }
    }

    internal static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    internal static FontFamily ResolveFontFamily(string? fontFamilyName)
    {
        if (!string.IsNullOrWhiteSpace(fontFamilyName))
        {
            try
            {
                return new FontFamily(fontFamilyName);
            }
            catch
            {
            }
        }

        return new FontFamily("Segoe UI");
    }

    internal static FontStyle GetPreferredFontStyle(FontFamily fontFamily)
    {
        if (fontFamily.IsStyleAvailable(FontStyle.Regular))
        {
            return FontStyle.Regular;
        }

        if (fontFamily.IsStyleAvailable(FontStyle.Bold))
        {
            return FontStyle.Bold;
        }

        if (fontFamily.IsStyleAvailable(FontStyle.Italic))
        {
            return FontStyle.Italic;
        }

        if (fontFamily.IsStyleAvailable(FontStyle.Strikeout))
        {
            return FontStyle.Strikeout;
        }

        if (fontFamily.IsStyleAvailable(FontStyle.Underline))
        {
            return FontStyle.Underline;
        }

        return FontStyle.Regular;
    }

    internal static Font CreateDisplayFont(string? fontFamilyName, float size, GraphicsUnit unit = GraphicsUnit.Pixel)
    {
        var fontFamily = ResolveFontFamily(fontFamilyName);
        var style = GetPreferredFontStyle(fontFamily);
        return new Font(fontFamily, size, style, unit);
    }

    internal static Color ApplyOpacity(Color color, double opacity)
    {
        var alpha = (int)Math.Clamp(Math.Round(opacity * 255), 0, 255);
        return Color.FromArgb(alpha, color);
    }

    internal static IReadOnlyList<string> EnsureGradientColors(IReadOnlyList<string>? colors)
    {
        if (colors is null || colors.Count == 0)
        {
            return new[] { "#FFFFFF", "#FFFFFF" };
        }

        if (colors.Count == 1)
        {
            return new[] { colors[0], colors[0] };
        }

        return new[] { colors[0], colors[1] };
    }

    internal static GraphicsPath CreateRoundedRectangle(Rectangle bounds, float radius)
    {
        var path = new GraphicsPath();
        var safeRadius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f));

        if (safeRadius <= 0)
        {
            path.AddRectangle(bounds);
            path.CloseFigure();
            return path;
        }

        var diameter = safeRadius * 2;
        var arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void CenterOnPrimaryScreen(Form form)
    {
        var bounds = Screen.PrimaryScreen?.WorkingArea ?? Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(
            bounds.Left + Math.Max(0, (bounds.Width - form.Width) / 2),
            bounds.Top + Math.Max(0, (bounds.Height - form.Height) / 2));
    }
}
