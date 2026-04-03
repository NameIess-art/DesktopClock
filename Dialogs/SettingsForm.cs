using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DesktopClock.Helpers;
using DesktopClock.Models;

namespace DesktopClock.Dialogs;

public sealed class SettingsForm : Form
{
    private readonly ClockSettings _settings;
    private readonly Action _onChanged;

    private readonly Button _backgroundNavButton = CreateNavButton("背景");
    private readonly Button _colorNavButton = CreateNavButton("颜色");
    private readonly Button _fontNavButton = CreateNavButton("字体");
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 18) };

    private readonly Panel _backgroundPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _colorPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _fontPanel = new() { Dock = DockStyle.Fill };

    private readonly TextBox _backgroundColorTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TrackBar _opacityTrackBar = new() { Minimum = 0, Maximum = 100, TickFrequency = 5, Dock = DockStyle.Fill };
    private readonly NumericUpDown _widthUpDown = new() { Minimum = 180, Maximum = 900, DecimalPlaces = 0, Dock = DockStyle.Fill };
    private readonly NumericUpDown _heightUpDown = new() { Minimum = 72, Maximum = 360, DecimalPlaces = 0, Dock = DockStyle.Fill };
    private readonly TrackBar _cornerRadiusTrackBar = new() { Minimum = 0, Maximum = 120, TickFrequency = 4, Dock = DockStyle.Fill };

    private readonly ComboBox _fillModeComboBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _primaryColorTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _secondaryColorTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button _secondaryColorButton = new() { Text = "选择...", Dock = DockStyle.Fill };
    private readonly ComboBox _gradientDirectionComboBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Panel _colorPreviewPanel = new() { Dock = DockStyle.Fill, Height = 120 };

    private readonly ListBox _fontListBox = new() { Dock = DockStyle.Fill };
    private readonly Panel _fontPreviewPanel = new() { Dock = DockStyle.Fill, Height = 120 };
    private readonly TextBox _fontSearchTextBox = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索字体" };
    private readonly Button _openFontsFolderButton = new() { Text = "字体文件夹", AutoSize = false, Dock = DockStyle.Fill, Width = 96 };
    private List<string> _allFontNames = new();

    private SettingsSection _activeSection;

    public SettingsForm(ClockSettings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;

        Text = "设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 460);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        _fillModeComboBox.Items.AddRange(new object[] { "纯色", "渐变" });
        _gradientDirectionComboBox.Items.AddRange(new object[] { "左右", "上下" });
        _colorPreviewPanel.Paint += OnColorPreviewPaint;
        _fontPreviewPanel.Paint += OnFontPreviewPaint;
        _fontListBox.SelectedIndexChanged += OnFontSelectedIndexChanged;
        _fontSearchTextBox.TextChanged += OnFontSearchTextChanged;

        Controls.Add(BuildLayout());
        BuildBackgroundPanel();
        BuildColorPanel();
        BuildFontPanel();
        LoadValues();
        ShowSection(SettingsSection.Background);

        Shown += (_, _) => DrawingHelpers.CenterOnPrimaryScreen(this);
    }

    public void ShowSection(SettingsSection section)
    {
        _activeSection = section;
        _backgroundPanel.Visible = section == SettingsSection.Background;
        _colorPanel.Visible = section == SettingsSection.Color;
        _fontPanel.Visible = section == SettingsSection.Font;

        ApplyNavState(_backgroundNavButton, section == SettingsSection.Background);
        ApplyNavState(_colorNavButton, section == SettingsSection.Color);
        ApplyNavState(_fontNavButton, section == SettingsSection.Font);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var navPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 14, 18, 10),
            BackColor = Color.FromArgb(245, 247, 250)
        };

        var navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _backgroundNavButton.Click += (_, _) => ShowSection(SettingsSection.Background);
        _colorNavButton.Click += (_, _) => ShowSection(SettingsSection.Color);
        _fontNavButton.Click += (_, _) => ShowSection(SettingsSection.Font);

        navFlow.Controls.Add(_backgroundNavButton);
        navFlow.Controls.Add(_colorNavButton);
        navFlow.Controls.Add(_fontNavButton);
        navPanel.Controls.Add(navFlow);

        _contentPanel.Controls.Add(_backgroundPanel);
        _contentPanel.Controls.Add(_colorPanel);
        _contentPanel.Controls.Add(_fontPanel);

        root.Controls.Add(navPanel, 0, 0);
        root.Controls.Add(_contentPanel, 0, 1);
        return root;
    }

    private static Button CreateNavButton(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = false,
            Width = 84,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 10, 0),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85)
        };
    }

    private static void ApplyNavState(Button button, bool isActive)
    {
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = isActive ? Color.FromArgb(15, 23, 42) : Color.White;
        button.ForeColor = isActive ? Color.White : Color.FromArgb(51, 65, 85);
    }

    private void BuildBackgroundPanel()
    {
        var layout = CreateEditorTable();
        AddRow(layout, 0, "背景颜色", _backgroundColorTextBox, new Button { Text = "选择...", Dock = DockStyle.Fill }, OnSelectBackgroundColorClick);
        AddRow(layout, 1, "透明度", _opacityTrackBar, null, null, 3);
        AddRow(layout, 2, "背景宽度", _widthUpDown, null, null, 3);
        AddRow(layout, 3, "背景高度", _heightUpDown, null, null, 3);
        AddRow(layout, 4, "圆角半径", _cornerRadiusTrackBar, null, null, 3);

        var tip = new Label
        {
            Dock = DockStyle.Fill,
            Text = "修改会立即应用到桌面时钟。",
            ForeColor = Color.FromArgb(71, 85, 105),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(tip, 0, 5);
        layout.SetColumnSpan(tip, 3);

        _opacityTrackBar.ValueChanged += (_, _) =>
        {
            if (!Visible)
            {
                return;
            }

            _settings.BackgroundOpacity = _opacityTrackBar.Value / 100.0;
            _onChanged();
        };

        _cornerRadiusTrackBar.ValueChanged += (_, _) =>
        {
            if (!Visible)
            {
                return;
            }

            _settings.CornerRadius = _cornerRadiusTrackBar.Value;
            _onChanged();
        };

        _widthUpDown.ValueChanged += (_, _) =>
        {
            if (!Visible)
            {
                return;
            }

            _settings.BackgroundWidth = (double)_widthUpDown.Value;
            _onChanged();
        };

        _heightUpDown.ValueChanged += (_, _) =>
        {
            if (!Visible)
            {
                return;
            }

            _settings.BackgroundHeight = (double)_heightUpDown.Value;
            _onChanged();
        };

        _backgroundPanel.Controls.Add(layout);
    }

    private void BuildColorPanel()
    {
        var layout = CreateEditorTable();
        AddRow(layout, 0, "填充模式", _fillModeComboBox, null, null);
        AddRow(layout, 1, "主颜色", _primaryColorTextBox, new Button { Text = "选择...", Dock = DockStyle.Fill }, (_, _) => PickColor(0));
        AddRow(layout, 2, "次颜色", _secondaryColorTextBox, _secondaryColorButton, (_, _) => PickColor(1));
        AddRow(layout, 3, "渐变方向", _gradientDirectionComboBox, null, null);

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_colorPreviewPanel, 0, 4);
        layout.SetColumnSpan(_colorPreviewPanel, 3);

        _fillModeComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!Visible || _fillModeComboBox.SelectedIndex < 0)
            {
                return;
            }

            _settings.FillMode = _fillModeComboBox.SelectedIndex == 0 ? TextFillMode.Solid : TextFillMode.Gradient;
            UpdateGradientControls();
            _colorPreviewPanel.Invalidate();
            _onChanged();
        };

        _gradientDirectionComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!Visible || _gradientDirectionComboBox.SelectedIndex < 0)
            {
                return;
            }

            _settings.GradientDirection = _gradientDirectionComboBox.SelectedIndex == 0
                ? GradientDirection.LeftToRight
                : GradientDirection.TopToBottom;
            _colorPreviewPanel.Invalidate();
            _onChanged();
        };

        _colorPanel.Controls.Add(layout);
    }

    private void BuildFontPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

        layout.Controls.Add(new Label
        {
            Text = "系统字体库",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(71, 85, 105),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var buttonHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        buttonHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        _openFontsFolderButton.Click += OnOpenFontsFolderButtonClick;
        buttonHost.Controls.Add(_fontSearchTextBox, 0, 0);
        buttonHost.Controls.Add(_openFontsFolderButton, 1, 0);
        layout.Controls.Add(buttonHost, 0, 1);

        layout.Controls.Add(_fontListBox, 0, 2);
        layout.Controls.Add(_fontPreviewPanel, 0, 3);
        _fontPanel.Controls.Add(layout);
    }

    private static TableLayoutPanel CreateEditorTable()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        return layout;
    }

    private static void AddRow(TableLayoutPanel layout, int rowIndex, string labelText, Control mainControl, Button? button, EventHandler? buttonHandler, int rowSpan = 1)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var label = new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(label, 0, rowIndex);
        layout.Controls.Add(mainControl, 1, rowIndex);
        layout.SetColumnSpan(mainControl, rowSpan - 1 <= 0 ? 1 : rowSpan - 1);

        if (button is null)
        {
            return;
        }

        button.Click += buttonHandler;
        layout.Controls.Add(button, 2, rowIndex);
    }

    private void LoadValues()
    {
        _backgroundColorTextBox.Text = _settings.BackgroundColor;
        _opacityTrackBar.Value = (int)Math.Round(_settings.BackgroundOpacity * 100);
        _widthUpDown.Value = (decimal)_settings.BackgroundWidth;
        _heightUpDown.Value = (decimal)_settings.BackgroundHeight;
        _cornerRadiusTrackBar.Value = (int)Math.Round(_settings.CornerRadius);

        var colors = DrawingHelpers.EnsureGradientColors(_settings.GradientColors);
        _primaryColorTextBox.Text = colors[0];
        _secondaryColorTextBox.Text = colors[1];
        _fillModeComboBox.SelectedIndex = _settings.FillMode == TextFillMode.Solid ? 0 : 1;
        _gradientDirectionComboBox.SelectedIndex = _settings.GradientDirection == GradientDirection.LeftToRight ? 0 : 1;
        UpdateGradientControls();
        _colorPreviewPanel.Invalidate();

        _allFontNames = FontFamily.Families
            .Select(static family => family.Name)
            .OrderBy(static name => name)
            .ToList();
        ApplyFontFilter();
        _fontPreviewPanel.Invalidate();
    }

    private void ApplyFontFilter()
    {
        var selectedFont = _fontListBox.SelectedItem as string ?? _settings.FontFamilyName;
        var keyword = _fontSearchTextBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allFontNames
            : _allFontNames
                .Where(name => name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _fontListBox.BeginUpdate();
        _fontListBox.Items.Clear();
        _fontListBox.Items.AddRange(filtered.Cast<object>().ToArray());
        _fontListBox.EndUpdate();

        if (!string.IsNullOrWhiteSpace(selectedFont) && filtered.Any(name => string.Equals(name, selectedFont, StringComparison.OrdinalIgnoreCase)))
        {
            _fontListBox.SelectedItem = filtered.First(name => string.Equals(name, selectedFont, StringComparison.OrdinalIgnoreCase));
        }
        else if (filtered.Count > 0)
        {
            _fontListBox.SelectedIndex = 0;
        }
    }

    private void OnSelectBackgroundColorClick(object? sender, EventArgs e)
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = DrawingHelpers.ParseColor(_settings.BackgroundColor, Color.Black)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.BackgroundColor = DrawingHelpers.ToHex(dialog.Color);
        _backgroundColorTextBox.Text = _settings.BackgroundColor;
        _onChanged();
    }

    private void PickColor(int index)
    {
        var colors = DrawingHelpers.EnsureGradientColors(_settings.GradientColors).ToArray();
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = DrawingHelpers.ParseColor(colors[index], Color.White)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        colors[index] = DrawingHelpers.ToHex(dialog.Color);
        _settings.GradientColors = colors.ToList();
        LoadValues();
        _onChanged();
        ShowSection(SettingsSection.Color);
    }

    private void UpdateGradientControls()
    {
        var isGradient = _settings.FillMode == TextFillMode.Gradient;
        _secondaryColorTextBox.Enabled = isGradient;
        _secondaryColorButton.Enabled = isGradient;
        _gradientDirectionComboBox.Enabled = isGradient;
    }

    private void OnColorPreviewPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Color.FromArgb(226, 232, 240));

        using var font = DrawingHelpers.CreateDisplayFont(_settings.FontFamilyName, 42, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var rect = _colorPreviewPanel.ClientRectangle;
        var colors = DrawingHelpers.EnsureGradientColors(_settings.GradientColors);
        var primary = DrawingHelpers.ParseColor(colors[0], Color.White);
        var secondary = DrawingHelpers.ParseColor(colors[1], Color.Gainsboro);

        if (_settings.FillMode == TextFillMode.Solid)
        {
            using var brush = new SolidBrush(primary);
            e.Graphics.DrawString("12:34", font, brush, rect, format);
            return;
        }

        using var gradientBrush = new LinearGradientBrush(
            rect,
            primary,
            secondary,
            _settings.GradientDirection == GradientDirection.LeftToRight ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical);
        e.Graphics.DrawString("12:34", font, gradientBrush, rect, format);
    }

    private void OnFontSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_fontListBox.SelectedItem is not string fontName)
        {
            return;
        }

        _settings.FontFamilyName = fontName;
        _fontPreviewPanel.Invalidate();
        _onChanged();
    }

    private void OnFontSearchTextChanged(object? sender, EventArgs e)
    {
        ApplyFontFilter();
    }

    private void OnFontPreviewPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(226, 232, 240));
        using var font = DrawingHelpers.CreateDisplayFont(_settings.FontFamilyName, 42, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString("12:34", font, brush, _fontPreviewPanel.ClientRectangle, format);
    }

    private void OnOpenFontsFolderButtonClick(object? sender, EventArgs e)
    {
        try
        {
            var fontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!string.IsNullOrWhiteSpace(fontsPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fontsPath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
        }
    }
}
