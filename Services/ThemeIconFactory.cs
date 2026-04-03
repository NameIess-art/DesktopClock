using System.Drawing;
using System.Drawing.Drawing2D;
using DesktopClock.Native;

namespace DesktopClock.Services;

public sealed class ThemeIconFactory
{
    public Icon CreateIcon(Color strokeColor)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(strokeColor, 5)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        using var brush = new SolidBrush(strokeColor);
        graphics.DrawEllipse(pen, 10, 10, 44, 44);
        graphics.DrawLine(pen, 32, 32, 32, 20);
        graphics.DrawLine(pen, 32, 32, 43, 37);
        graphics.FillEllipse(brush, 28, 28, 8, 8);

        var iconHandle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(iconHandle).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }
}
