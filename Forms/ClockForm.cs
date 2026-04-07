using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using DesktopClock.Helpers;
using DesktopClock.Models;
using DesktopClock.Native;
using DesktopClock.Services;

namespace DesktopClock.Forms;

public sealed class ClockForm : Form
{
    private const double MinimumScale = 0.5;
    private const double MaximumScale = 4.0;
    private const float HandleSize = 12f;
    private const double ResizeSensitivity = 280.0;
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    private readonly ClockSettings _settings = ClockSettings.CreateDefault();

    private string _timeText = "00:00";
    private bool _isDragging;
    private bool _isResizing;
    private Point _dragStartCursor;
    private Point _dragStartLocation;
    private double _resizeStartScale;
    private Point _resizeStartCursor;
    private ResizeCorner _resizeCorner = ResizeCorner.None;

    public ClockForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = false;

        MouseDown += OnClockMouseDown;
        MouseMove += OnClockMouseMove;
        MouseUp += OnClockMouseUp;
        MouseWheel += OnClockMouseWheel;
        HandleCreated += OnClockHandleCreated;
    }

    public event EventHandler? TransformCommitted;

    public double CurrentScale => _settings.Scale;

    protected override bool ShowWithoutActivation => !_settings.IsEditMode;

    public void Initialize(ClockSettings settings)
    {
        CopySettings(settings);
        ApplySettings(_settings);
    }

    public void ApplySettings(ClockSettings settings)
    {
        CopySettings(settings);
        UpdateFormBounds();
        ApplyInteractionMode();
        RenderLayeredWindow();
    }

    public void AttachToDesktop(DesktopLayerService desktopLayerService)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        desktopLayerService.TryAttachToDesktop(Handle);
    }

    public void UpdateDisplayedTime(string timeText)
    {
        if (string.Equals(_timeText, timeText, StringComparison.Ordinal))
        {
            return;
        }

        _timeText = timeText;
        RenderLayeredWindow();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RenderLayeredWindow();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RenderLayeredWindow();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= (int)NativeMethods.WS_EX_LAYERED;
            return createParams;
        }
    }

    private void RenderLayeredWindow()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0)
        {
            return;
        }

        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            var rect = new Rectangle(0, 0, bitmap.Width - 1, bitmap.Height - 1);
            using var backgroundPath = DrawingHelpers.CreateRoundedRectangle(rect, GetScaledCornerRadius());

            if (_settings.BackgroundOpacity > 0.001)
            {
                using var backgroundBrush = new SolidBrush(DrawingHelpers.ApplyOpacity(
                    DrawingHelpers.ParseColor(_settings.BackgroundColor, Color.FromArgb(15, 23, 42)),
                    _settings.BackgroundOpacity));
                graphics.FillPath(backgroundBrush, backgroundPath);
            }

            DrawClockText(graphics, rect);

            if (_settings.IsEditMode)
            {
                using var borderPen = new Pen(Color.FromArgb(180, 255, 255, 255), 1);
                graphics.DrawPath(borderPen, backgroundPath);
                DrawHandles(graphics);

                using var infoBrush = new SolidBrush(Color.FromArgb(245, 248, 250));
                using var infoFont = new Font("Microsoft YaHei UI", Math.Max(9f, (float)(10 * _settings.Scale)), FontStyle.Regular, GraphicsUnit.Pixel);
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var infoRect = new RectangleF(0, 6, Width, 18);
                graphics.DrawString("编辑模式  拖动移动，滚轮或角点缩放", infoFont, infoBrush, infoRect, format);
            }
        }

        ApplyBitmap(bitmap);
    }

    private void ApplyBitmap(Bitmap bitmap)
    {
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        var oldBitmap = IntPtr.Zero;

        try
        {
            oldBitmap = NativeMethods.SelectObject(memoryDc, hBitmap);

            var dstPoint = new NativeMethods.Point(Left, Top);
            var size = new NativeMethods.Size(bitmap.Width, bitmap.Height);
            var srcPoint = new NativeMethods.Point(0, 0);
            var blend = new NativeMethods.BlendFunction
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            NativeMethods.UpdateLayeredWindow(
                Handle,
                screenDc,
                ref dstPoint,
                ref size,
                memoryDc,
                ref srcPoint,
                0,
                ref blend,
                NativeMethods.ULW_ALPHA);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, oldBitmap);
            }

            NativeMethods.DeleteObject(hBitmap);
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void DrawClockText(Graphics graphics, Rectangle bounds)
    {
        var visibleItems = GetVisibleItems();
        if (visibleItems.Count == 0)
        {
            return;
        }

        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };

        for (var index = 0; index < visibleItems.Count; index++)
        {
            var item = visibleItems[index];
            var element = _settings.GetElementSettings(item);
            var text = GetDisplayText(item);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var textBounds = GetElementBounds(bounds, element, visibleItems.Count, index, item);
            using var font = DrawingHelpers.CreateDisplayFont(element.FontFamilyName, GetElementFontSize(item, visibleItems.Count), GraphicsUnit.Pixel);
            using var brush = CreateTextBrush(bounds, element);
            graphics.DrawString(text, font, brush, textBounds, format);
        }
    }

    private Brush CreateTextBrush(Rectangle bounds, ClockElementSettings element)
    {
        if (element.FillMode == TextFillMode.Solid)
        {
            return new SolidBrush(DrawingHelpers.ParseColor(
                DrawingHelpers.EnsureGradientColors(element.GradientColors)[0],
                Color.White));
        }

        var colors = DrawingHelpers.EnsureGradientColors(element.GradientColors);
        var start = DrawingHelpers.ParseColor(colors[0], Color.White);
        var end = DrawingHelpers.ParseColor(colors[1], Color.Gainsboro);
        var mode = element.GradientDirection == GradientDirection.LeftToRight
            ? LinearGradientMode.Horizontal
            : LinearGradientMode.Vertical;

        return new LinearGradientBrush(bounds, start, end, mode);
    }

    private void DrawHandles(Graphics graphics)
    {
        foreach (var rect in GetHandleRectangles())
        {
            using var brush = new SolidBrush(Color.White);
            graphics.FillRectangle(brush, rect);
            using var pen = new Pen(Color.FromArgb(120, 15, 23, 42), 1);
            graphics.DrawRectangle(pen, Rectangle.Round(rect));
        }
    }

    private IEnumerable<RectangleF> GetHandleRectangles()
    {
        yield return new RectangleF(4, 4, HandleSize, HandleSize);
        yield return new RectangleF(Width - HandleSize - 4, 4, HandleSize, HandleSize);
        yield return new RectangleF(4, Height - HandleSize - 4, HandleSize, HandleSize);
        yield return new RectangleF(Width - HandleSize - 4, Height - HandleSize - 4, HandleSize, HandleSize);
    }

    private float GetElementFontSize(ClockDisplayItem item, int visibleCount)
    {
        if (item == ClockDisplayItem.Time)
        {
            var previewText = ClockFormatHelpers.FormatDateTime(
                new DateTime(2026, 4, 4, 12, 34, 56),
                _settings.TimeElement.CustomFormat,
                ClockFormatHelpers.GetFallbackTimeFormat(_settings.DisplayFormat),
                CultureInfo.CurrentCulture);
            var divisor = previewText.Length switch
            {
                <= 5 => 1.55f,
                <= 8 => 2.0f,
                <= 12 => 2.45f,
                _ => 2.9f
            };
            var baseSize = Math.Max(44f, Height / divisor);
            return visibleCount > 1 ? Math.Max(32f, baseSize * 0.74f) : baseSize;
        }

        var ratio = visibleCount switch
        {
            1 => 0.24f,
            2 => 0.18f,
            _ => 0.145f
        };
        return Math.Max(14f, Height * ratio);
    }

    private float GetScaledCornerRadius()
    {
        var scaled = _settings.CornerRadius * _settings.Scale;
        return (float)Math.Min(scaled, Math.Min(Width, Height) / 2.0);
    }

    private void UpdateFormBounds()
    {
        Size = new Size(
            Math.Max(120, (int)Math.Round(_settings.BackgroundWidth * _settings.Scale)),
            Math.Max(60, (int)Math.Round(_settings.BackgroundHeight * _settings.Scale)));

        Left = (int)Math.Round(_settings.WindowLeft);
        Top = (int)Math.Round(_settings.WindowTop);
    }

    private void ApplyInteractionMode()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var styles = NativeMethods.GetExtendedWindowStyle(Handle);
        styles |= NativeMethods.WS_EX_TOOLWINDOW;
        styles |= NativeMethods.WS_EX_LAYERED;
        styles &= ~NativeMethods.WS_EX_APPWINDOW;

        if (_settings.IsEditMode)
        {
            styles &= ~NativeMethods.WS_EX_TRANSPARENT;
            styles &= ~NativeMethods.WS_EX_NOACTIVATE;
        }
        else
        {
            styles |= NativeMethods.WS_EX_TRANSPARENT;
            styles |= NativeMethods.WS_EX_NOACTIVATE;
        }

        NativeMethods.SetExtendedWindowStyle(Handle, styles);
        NativeMethods.RefreshWindowFrame(Handle);
    }

    private void CopySettings(ClockSettings settings)
    {
        _settings.WindowLeft = settings.WindowLeft;
        _settings.WindowTop = settings.WindowTop;
        _settings.Scale = settings.Scale;
        _settings.IsEditMode = settings.IsEditMode;
        _settings.DisplayFormat = settings.DisplayFormat;
        _settings.TimeElement = CloneElement(settings.TimeElement);
        _settings.DateElement = CloneElement(settings.DateElement);
        _settings.WeekdayElement = CloneElement(settings.WeekdayElement);
        _settings.ShowWeekday = settings.ShowWeekday;
        _settings.ShowDate = settings.ShowDate;
        _settings.BackgroundColor = settings.BackgroundColor;
        _settings.BackgroundOpacity = settings.BackgroundOpacity;
        _settings.BackgroundWidth = settings.BackgroundWidth;
        _settings.BackgroundHeight = settings.BackgroundHeight;
        _settings.CornerRadius = settings.CornerRadius;
        _settings.FontFamilyName = settings.FontFamilyName;
        _settings.FillMode = settings.FillMode;
        _settings.GradientColors = settings.GradientColors.ToList();
        _settings.GradientDirection = settings.GradientDirection;
        _settings.LaunchAtStartup = settings.LaunchAtStartup;
    }

    private void OnClockHandleCreated(object? sender, EventArgs e)
    {
        ApplyInteractionMode();
        RenderLayeredWindow();
    }

    private void OnClockMouseDown(object? sender, MouseEventArgs e)
    {
        if (!_settings.IsEditMode || e.Button != MouseButtons.Left)
        {
            return;
        }

        _resizeCorner = HitTestResizeCorner(e.Location);
        if (_resizeCorner != ResizeCorner.None)
        {
            _isResizing = true;
            _resizeStartCursor = Cursor.Position;
            _resizeStartScale = _settings.Scale;
            return;
        }

        _isDragging = true;
        _dragStartCursor = Cursor.Position;
        _dragStartLocation = Location;
    }

    private void OnClockMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_settings.IsEditMode)
        {
            Cursor = Cursors.Default;
            return;
        }

        if (_isDragging)
        {
            var delta = new Size(Cursor.Position.X - _dragStartCursor.X, Cursor.Position.Y - _dragStartCursor.Y);
            Location = new Point(_dragStartLocation.X + delta.Width, _dragStartLocation.Y + delta.Height);
            return;
        }

        if (_isResizing)
        {
            var dx = Cursor.Position.X - _resizeStartCursor.X;
            var dy = Cursor.Position.Y - _resizeStartCursor.Y;
            var delta = _resizeCorner switch
            {
                ResizeCorner.TopLeft => (-dx - dy) / 2.0,
                ResizeCorner.TopRight => (dx - dy) / 2.0,
                ResizeCorner.BottomLeft => (-dx + dy) / 2.0,
                ResizeCorner.BottomRight => (dx + dy) / 2.0,
                _ => 0
            };

            UpdateScale(_resizeStartScale + (delta / ResizeSensitivity));
            return;
        }

        Cursor = HitTestResizeCorner(e.Location) switch
        {
            ResizeCorner.TopLeft => Cursors.SizeNWSE,
            ResizeCorner.BottomRight => Cursors.SizeNWSE,
            ResizeCorner.TopRight => Cursors.SizeNESW,
            ResizeCorner.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.SizeAll
        };
    }

    private void OnClockMouseUp(object? sender, MouseEventArgs e)
    {
        var shouldCommit = _isDragging || _isResizing;
        _isDragging = false;
        _isResizing = false;
        _resizeCorner = ResizeCorner.None;

        if (shouldCommit)
        {
            CommitTransform();
        }
    }

    private void OnClockMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_settings.IsEditMode)
        {
            return;
        }

        UpdateScale(_settings.Scale + (Math.Sign(e.Delta) * 0.05));
        CommitTransform();
    }

    private void UpdateScale(double scale)
    {
        _settings.Scale = Math.Clamp(scale, MinimumScale, MaximumScale);
        UpdateFormBounds();
        RenderLayeredWindow();
    }

    private void CommitTransform()
    {
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        TransformCommitted?.Invoke(this, EventArgs.Empty);
    }

    private ResizeCorner HitTestResizeCorner(Point location)
    {
        var handles = GetHandleRectangles().ToArray();

        if (handles[0].Contains(location))
        {
            return ResizeCorner.TopLeft;
        }

        if (handles[1].Contains(location))
        {
            return ResizeCorner.TopRight;
        }

        if (handles[2].Contains(location))
        {
            return ResizeCorner.BottomLeft;
        }

        if (handles[3].Contains(location))
        {
            return ResizeCorner.BottomRight;
        }

        return ResizeCorner.None;
    }

    private IReadOnlyList<ClockDisplayItem> GetVisibleItems()
    {
        var items = new List<ClockDisplayItem>(3);

        if (_settings.TimeElement.IsVisible)
        {
            items.Add(ClockDisplayItem.Time);
        }

        if (_settings.WeekdayElement.IsVisible)
        {
            items.Add(ClockDisplayItem.Weekday);
        }

        if (_settings.DateElement.IsVisible)
        {
            items.Add(ClockDisplayItem.Date);
        }

        return items;
    }

    private string GetDisplayText(ClockDisplayItem item)
    {
        var now = DateTime.Now;
        return item switch
        {
            ClockDisplayItem.Time => _timeText,
            ClockDisplayItem.Date => FormatDateTime(now, _settings.DateElement.CustomFormat, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ClockDisplayItem.Weekday => FormatDateTime(now, _settings.WeekdayElement.CustomFormat, "dddd", ChineseCulture),
            _ => string.Empty
        };
    }

    private RectangleF GetElementBounds(Rectangle bounds, ClockElementSettings element, int visibleCount, int itemIndex, ClockDisplayItem item)
    {
        var ySlots = visibleCount switch
        {
            1 => new[] { 0.50f },
            2 => new[] { 0.36f, 0.70f },
            _ => new[] { 0.28f, 0.58f, 0.78f }
        };

        var slotCenterY = ySlots[Math.Min(itemIndex, ySlots.Length - 1)];
        var topInset = _settings.IsEditMode ? 22f : 0f;
        var availableHeight = bounds.Height - topInset;
        var centerY = topInset + (availableHeight * slotCenterY);
        var rectHeight = item == ClockDisplayItem.Time
            ? (visibleCount == 1 ? bounds.Height * 0.56f : bounds.Height * 0.34f)
            : (visibleCount == 1 ? bounds.Height * 0.24f : bounds.Height * 0.16f);
        var rectWidth = item == ClockDisplayItem.Time ? bounds.Width * 0.96f : bounds.Width * 0.84f;
        var offsetX = (float)(_settings.Scale * element.OffsetX);
        var offsetY = (float)(_settings.Scale * element.OffsetY);

        return new RectangleF(
            ((bounds.Width - rectWidth) / 2f) + offsetX,
            centerY - (rectHeight / 2f) + offsetY,
            rectWidth,
            rectHeight);
    }

    private static ClockElementSettings CloneElement(ClockElementSettings source)
    {
        return new ClockElementSettings
        {
            IsVisible = source.IsVisible,
            CustomFormat = source.CustomFormat,
            FontFamilyName = source.FontFamilyName,
            FillMode = source.FillMode,
            GradientColors = source.GradientColors.ToList(),
            GradientDirection = source.GradientDirection,
            OffsetX = source.OffsetX,
            OffsetY = source.OffsetY
        };
    }

    private static string FormatDateTime(DateTime value, string? customFormat, string fallbackFormat, IFormatProvider provider)
    {
        if (string.IsNullOrWhiteSpace(customFormat))
        {
            return value.ToString(fallbackFormat, provider);
        }

        try
        {
            return value.ToString(customFormat, provider);
        }
        catch (FormatException)
        {
            return value.ToString(fallbackFormat, provider);
        }
    }

    private enum ResizeCorner
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
