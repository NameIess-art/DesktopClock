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
    private const float MinFontSize = 10f;
    private const float BaseContentWidth = 320f;
    private const float BaseContentHeight = 120f;
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
                DrawEditModeHint(graphics, rect);
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

        var contentBounds = GetContentBounds(bounds);

        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox,
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

            var textBounds = GetElementBounds(contentBounds, visibleItems, index, item);
            var drawBounds = InsetBounds(
                textBounds,
                Math.Max(4f, textBounds.Width * 0.02f),
                Math.Max(2f, textBounds.Height * 0.08f));
            var preferredFontSize = GetElementFontSize(item, visibleItems.Count, contentBounds);
            var fittedFontSize = GetFittedFontSize(graphics, element.FontFamilyName, text, drawBounds, preferredFontSize);

            using var font = DrawingHelpers.CreateDisplayFont(element.FontFamilyName, fittedFontSize, GraphicsUnit.Pixel);
            using var brush = CreateTextBrush(Rectangle.Round(drawBounds), element);
            graphics.DrawString(text, font, brush, drawBounds, format);
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

    private float GetElementFontSize(ClockDisplayItem item, int visibleCount, RectangleF layoutBounds)
    {
        var layoutHeight = Math.Max(60f, layoutBounds.Height);

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
            var baseSize = Math.Max(44f, layoutHeight / divisor);
            var scaledSize = visibleCount > 1 ? Math.Max(32f, baseSize * 0.74f) : baseSize;
            return scaledSize * (float)_settings.TimeElement.SizeScale;
        }

        var ratio = visibleCount switch
        {
            1 => 0.24f,
            2 => 0.18f,
            _ => 0.145f
        };
        return Math.Max(14f, layoutHeight * ratio) * (float)_settings.GetElementSettings(item).SizeScale;
    }

    private float GetFittedFontSize(Graphics graphics, string? fontFamilyName, string text, RectangleF bounds, float preferredFontSize)
    {
        if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 1 || bounds.Height <= 1)
        {
            return MinFontSize;
        }

        var low = MinFontSize;
        var high = Math.Max(MinFontSize, preferredFontSize);

        for (var iteration = 0; iteration < 10; iteration++)
        {
            var size = (low + high) / 2f;
            using var font = DrawingHelpers.CreateDisplayFont(fontFamilyName, size, GraphicsUnit.Pixel);
            var measured = MeasureText(graphics, text, font);

            if (measured.Width <= bounds.Width && measured.Height <= bounds.Height)
            {
                low = size;
            }
            else
            {
                high = size;
            }
        }

        return Math.Max(MinFontSize, low);
    }

    private static SizeF MeasureText(Graphics graphics, string text, Font font)
    {
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
        return graphics.MeasureString(text, font, SizeF.Empty, format);
    }

    private float GetScaledCornerRadius()
    {
        var scaled = _settings.CornerRadius * _settings.Scale;
        return (float)Math.Min(scaled, Math.Min(Width, Height) / 2.0);
    }

    private RectangleF GetContentBounds(Rectangle bounds)
    {
        var scale = (float)_settings.Scale;
        var horizontalPadding = BaseContentWidth * 0.02f * scale;
        var verticalPadding = BaseContentHeight * 0.04f * scale;
        var contentWidth = BaseContentWidth * 0.96f * scale;
        var contentHeight = BaseContentHeight * 0.92f * scale;

        return new RectangleF(
            bounds.Left + horizontalPadding,
            bounds.Top + verticalPadding,
            contentWidth,
            contentHeight);
    }

    private void UpdateFormBounds()
    {
        var minimumWidth = (int)Math.Round(BaseContentWidth * (float)_settings.Scale);
        var minimumHeight = (int)Math.Round(BaseContentHeight * (float)_settings.Scale);

        Size = new Size(
            Math.Max(minimumWidth, (int)Math.Round(_settings.BackgroundWidth * _settings.Scale)),
            Math.Max(minimumHeight, (int)Math.Round(_settings.BackgroundHeight * _settings.Scale)));

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

    private RectangleF GetElementBounds(RectangleF bounds, IReadOnlyList<ClockDisplayItem> visibleItems, int itemIndex, ClockDisplayItem item)
    {
        var element = _settings.GetElementSettings(item);
        var scale = Math.Max(0.3f, (float)element.SizeScale);
        var anchorY = GetElementAnchorY(bounds, visibleItems.Count, itemIndex);
        var baseHeight = GetElementBaseHeight(bounds.Height, visibleItems.Count, item);
        var baseWidth = item == ClockDisplayItem.Time ? bounds.Width * 0.96f : bounds.Width * 0.84f;
        var rectHeight = Math.Min(bounds.Height * 0.9f, Math.Max(24f, baseHeight * scale));
        var rectWidth = Math.Min(bounds.Width * 0.98f, Math.Max(80f, baseWidth * (0.85f + ((scale - 1f) * 0.2f))));
        var offsetX = (float)(_settings.Scale * element.OffsetX);
        var offsetY = (float)(_settings.Scale * element.OffsetY);
        var left = bounds.Left + ((bounds.Width - rectWidth) / 2f) + offsetX;
        var top = anchorY - (rectHeight / 2f) + offsetY;

        return new RectangleF(
            left,
            Math.Clamp(top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - rectHeight)),
            rectWidth,
            rectHeight);
    }

    private static float GetElementAnchorY(RectangleF bounds, int visibleCount, int itemIndex)
    {
        var anchors = visibleCount switch
        {
            <= 1 => new[] { 0.5f },
            2 => new[] { 0.37f, 0.7f },
            _ => new[] { 0.3f, 0.58f, 0.8f }
        };

        var safeIndex = Math.Clamp(itemIndex, 0, anchors.Length - 1);
        return bounds.Top + (bounds.Height * anchors[safeIndex]);
    }

    private static float GetElementBaseHeight(float boundsHeight, int visibleCount, ClockDisplayItem item)
    {
        return (visibleCount, item) switch
        {
            (<= 1, ClockDisplayItem.Time) => boundsHeight * 0.68f,
            (<= 1, _) => boundsHeight * 0.32f,
            (2, ClockDisplayItem.Time) => boundsHeight * 0.42f,
            (2, _) => boundsHeight * 0.24f,
            (_, ClockDisplayItem.Time) => boundsHeight * 0.34f,
            _ => boundsHeight * 0.18f
        };
    }

    private void DrawEditModeHint(Graphics graphics, Rectangle bounds)
    {
        const string hintText = "编辑模式";
        using var font = new Font("Microsoft YaHei UI", Math.Max(9f, (float)(10 * _settings.Scale)), FontStyle.Regular, GraphicsUnit.Pixel);
        var measured = MeasureText(graphics, hintText, font);
        var badgeWidth = Math.Max(78f, measured.Width + 20f);
        var badgeHeight = Math.Max(22f, measured.Height + 8f);
        var badgeRect = new RectangleF(bounds.Right - badgeWidth - 10f, 8f, badgeWidth, badgeHeight);

        using var badgePath = DrawingHelpers.CreateRoundedRectangle(Rectangle.Round(badgeRect), badgeHeight / 2f);
        using var badgeBrush = new SolidBrush(Color.FromArgb(96, 15, 23, 42));
        using var textBrush = new SolidBrush(Color.FromArgb(245, 248, 250));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        graphics.FillPath(badgeBrush, badgePath);
        graphics.DrawString(hintText, font, textBrush, badgeRect, format);
    }

    private static RectangleF InsetBounds(RectangleF bounds, float horizontalInset, float verticalInset)
    {
        var safeHorizontalInset = Math.Min(horizontalInset, Math.Max(0f, (bounds.Width - 1f) / 2f));
        var safeVerticalInset = Math.Min(verticalInset, Math.Max(0f, (bounds.Height - 1f) / 2f));

        return RectangleF.FromLTRB(
            bounds.Left + safeHorizontalInset,
            bounds.Top + safeVerticalInset,
            bounds.Right - safeHorizontalInset,
            bounds.Bottom - safeVerticalInset);
    }

    private static ClockElementSettings CloneElement(ClockElementSettings source)
    {
        return new ClockElementSettings
        {
            IsVisible = source.IsVisible,
            CustomFormat = source.CustomFormat,
            FontFamilyName = source.FontFamilyName,
            SizeScale = source.SizeScale,
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
