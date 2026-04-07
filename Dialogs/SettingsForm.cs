using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using DesktopClock.Helpers;
using DesktopClock.Models;

namespace DesktopClock.Dialogs;

public sealed class SettingsForm : Form
{
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    private readonly ClockSettings _settings;
    private readonly Action _onChanged;

    private readonly Button _backgroundNavButton = CreateNavButton("背景");
    private readonly Button _displayNavButton = CreateNavButton("显示");
    private readonly Button _positionNavButton = CreateNavButton("位置");
    private readonly Button _colorNavButton = CreateNavButton("颜色");
    private readonly Button _fontNavButton = CreateNavButton("字体");
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 18) };

    private readonly Panel _backgroundPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _displayPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _positionPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _colorPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _fontPanel = new() { Dock = DockStyle.Fill };

    private readonly TextBox _backgroundColorTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TrackBar _opacityTrackBar = new() { Minimum = 0, Maximum = 100, TickFrequency = 5, Dock = DockStyle.Fill };
    private readonly NumericUpDown _widthUpDown = new() { Minimum = 180, Maximum = 900, DecimalPlaces = 0, Dock = DockStyle.Fill };
    private readonly NumericUpDown _heightUpDown = new() { Minimum = 72, Maximum = 360, DecimalPlaces = 0, Dock = DockStyle.Fill };
    private readonly TrackBar _cornerRadiusTrackBar = new() { Minimum = 0, Maximum = 120, TickFrequency = 4, Dock = DockStyle.Fill };

    private readonly ComboBox _displayTargetComboBox = CreateComponentComboBox();
    private readonly CheckBox _visibleCheckBox = new() { Dock = DockStyle.Fill, Text = "显示该组件" };
    private readonly NumericUpDown _sizeScaleUpDown = new() { Minimum = 0.30M, Maximum = 4.00M, DecimalPlaces = 2, Increment = 0.05M, Dock = DockStyle.Fill };
    private readonly ComboBox _timeFormatComboBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _customFormatTextBox = new() { Dock = DockStyle.Fill, PlaceholderText = "输入自定义格式" };
    private readonly Label _displayHintLabel = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(71, 85, 105), TextAlign = ContentAlignment.MiddleLeft };

    private readonly ComboBox _positionTargetComboBox = CreateComponentComboBox();
    private readonly NumericUpDown _offsetXUpDown = new() { Minimum = -500, Maximum = 500, DecimalPlaces = 0, Dock = DockStyle.Fill };
    private readonly NumericUpDown _offsetYUpDown = new() { Minimum = -500, Maximum = 500, DecimalPlaces = 0, Dock = DockStyle.Fill };
    private readonly Label _positionHintLabel = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(71, 85, 105), TextAlign = ContentAlignment.MiddleLeft };

    private readonly ComboBox _colorTargetComboBox = CreateComponentComboBox();
    private readonly ComboBox _fillModeComboBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _primaryColorTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _secondaryColorTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button _secondaryColorButton = new() { Text = "选择...", Dock = DockStyle.Fill };
    private readonly ComboBox _gradientDirectionComboBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Panel _colorPreviewPanel = new() { Dock = DockStyle.Fill, Height = 120 };

    private readonly ComboBox _fontTargetComboBox = CreateComponentComboBox();
    private readonly ListBox _fontListBox = new() { Dock = DockStyle.Fill };
    private readonly Panel _fontPreviewPanel = new() { Dock = DockStyle.Fill, Height = 120 };
    private readonly TextBox _fontSearchTextBox = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索字体" };
    private readonly Button _openFontsFolderButton = new() { Text = "字体文件夹", AutoSize = false, Dock = DockStyle.Fill, Width = 100 };

    private readonly List<string> _allFontNames = new();
    private ClockDisplayItem _selectedItem = ClockDisplayItem.Time;
    private bool _isLoadingValues;

    public SettingsForm(ClockSettings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;

        Text = "设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(580, 520);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        _timeFormatComboBox.Items.AddRange(new object[] { "HH:mm", "HH:mm:ss", "自定义" });
        _fillModeComboBox.Items.AddRange(new object[] { "纯色", "渐变" });
        _gradientDirectionComboBox.Items.AddRange(new object[] { "左右", "上下" });

        _displayTargetComboBox.SelectedIndexChanged += OnComponentTargetChanged;
        _positionTargetComboBox.SelectedIndexChanged += OnComponentTargetChanged;
        _colorTargetComboBox.SelectedIndexChanged += OnComponentTargetChanged;
        _fontTargetComboBox.SelectedIndexChanged += OnComponentTargetChanged;

        _visibleCheckBox.CheckedChanged += OnVisibleCheckBoxChanged;
        _sizeScaleUpDown.ValueChanged += OnSizeScaleChanged;
        _timeFormatComboBox.SelectedIndexChanged += OnTimeFormatChanged;
        _customFormatTextBox.TextChanged += OnCustomFormatChanged;
        _offsetXUpDown.ValueChanged += OnOffsetValueChanged;
        _offsetYUpDown.ValueChanged += OnOffsetValueChanged;
        _fillModeComboBox.SelectedIndexChanged += OnFillModeChanged;
        _gradientDirectionComboBox.SelectedIndexChanged += OnGradientDirectionChanged;
        _fontSearchTextBox.TextChanged += (_, _) => ApplyFontFilter();
        _fontListBox.SelectedIndexChanged += OnFontSelectedIndexChanged;
        _openFontsFolderButton.Click += OnOpenFontsFolderButtonClick;
        _colorPreviewPanel.Paint += OnColorPreviewPaint;
        _fontPreviewPanel.Paint += OnFontPreviewPaint;

        Controls.Add(BuildLayout());
        BuildBackgroundPanel();
        BuildDisplayPanel();
        BuildPositionPanel();
        BuildColorPanel();
        BuildFontPanel();
        LoadValues();
        ShowSection(SettingsSection.Background);

        Shown += (_, _) => DrawingHelpers.CenterOnPrimaryScreen(this);
    }

    public void ShowSection(SettingsSection section)
    {
        _backgroundPanel.Visible = section == SettingsSection.Background;
        _displayPanel.Visible = section == SettingsSection.Display;
        _positionPanel.Visible = section == SettingsSection.Position;
        _colorPanel.Visible = section == SettingsSection.Color;
        _fontPanel.Visible = section == SettingsSection.Font;

        ApplyNavState(_backgroundNavButton, section == SettingsSection.Background);
        ApplyNavState(_displayNavButton, section == SettingsSection.Display);
        ApplyNavState(_positionNavButton, section == SettingsSection.Position);
        ApplyNavState(_colorNavButton, section == SettingsSection.Color);
        ApplyNavState(_fontNavButton, section == SettingsSection.Font);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
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
        _displayNavButton.Click += (_, _) => ShowSection(SettingsSection.Display);
        _positionNavButton.Click += (_, _) => ShowSection(SettingsSection.Position);
        _colorNavButton.Click += (_, _) => ShowSection(SettingsSection.Color);
        _fontNavButton.Click += (_, _) => ShowSection(SettingsSection.Font);

        navFlow.Controls.Add(_backgroundNavButton);
        navFlow.Controls.Add(_displayNavButton);
        navFlow.Controls.Add(_positionNavButton);
        navFlow.Controls.Add(_colorNavButton);
        navFlow.Controls.Add(_fontNavButton);
        navPanel.Controls.Add(navFlow);

        _contentPanel.Controls.Add(_backgroundPanel);
        _contentPanel.Controls.Add(_displayPanel);
        _contentPanel.Controls.Add(_positionPanel);
        _contentPanel.Controls.Add(_colorPanel);
        _contentPanel.Controls.Add(_fontPanel);

        root.Controls.Add(navPanel, 0, 0);
        root.Controls.Add(_contentPanel, 0, 1);
        return root;
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
            if (_isLoadingValues) return;
            _settings.BackgroundOpacity = _opacityTrackBar.Value / 100.0;
            NotifyChanged();
        };

        _cornerRadiusTrackBar.ValueChanged += (_, _) =>
        {
            if (_isLoadingValues) return;
            _settings.CornerRadius = _cornerRadiusTrackBar.Value;
            NotifyChanged();
        };

        _widthUpDown.ValueChanged += (_, _) =>
        {
            if (_isLoadingValues) return;
            _settings.BackgroundWidth = (double)_widthUpDown.Value;
            NotifyChanged();
        };

        _heightUpDown.ValueChanged += (_, _) =>
        {
            if (_isLoadingValues) return;
            _settings.BackgroundHeight = (double)_heightUpDown.Value;
            NotifyChanged();
        };

        _backgroundPanel.Controls.Add(layout);
    }

    private void BuildDisplayPanel()
    {
        var layout = CreateEditorTable();
        AddRow(layout, 4, "组件大小", _sizeScaleUpDown, null, null, 3);
        AddRow(layout, 0, "编辑组件", _displayTargetComboBox, null, null, 3);
        AddRow(layout, 1, "显示状态", _visibleCheckBox, null, null, 3);
        AddRow(layout, 2, "时间格式", _timeFormatComboBox, null, null, 3);
        AddRow(layout, 3, "自定义格式", _customFormatTextBox, null, null, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_displayHintLabel, 0, 5);
        layout.SetColumnSpan(_displayHintLabel, 3);
        _displayPanel.Controls.Add(layout);
    }

    private void BuildPositionPanel()
    {
        var layout = CreateEditorTable();
        AddRow(layout, 0, "编辑组件", _positionTargetComboBox, null, null, 3);
        AddRow(layout, 1, "水平偏移", _offsetXUpDown, null, null, 3);
        AddRow(layout, 2, "垂直偏移", _offsetYUpDown, null, null, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_positionHintLabel, 0, 3);
        layout.SetColumnSpan(_positionHintLabel, 3);
        _positionPanel.Controls.Add(layout);
    }

    private void BuildColorPanel()
    {
        var layout = CreateEditorTable();
        AddRow(layout, 0, "编辑组件", _colorTargetComboBox, null, null, 3);
        AddRow(layout, 1, "填充模式", _fillModeComboBox, null, null, 3);
        AddRow(layout, 2, "主颜色", _primaryColorTextBox, new Button { Text = "选择...", Dock = DockStyle.Fill }, (_, _) => PickColor(0));
        AddRow(layout, 3, "副颜色", _secondaryColorTextBox, _secondaryColorButton, (_, _) => PickColor(1));
        AddRow(layout, 4, "渐变方向", _gradientDirectionComboBox, null, null, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_colorPreviewPanel, 0, 5);
        layout.SetColumnSpan(_colorPreviewPanel, 3);
        _colorPanel.Controls.Add(layout);
    }

    private void BuildFontPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

        layout.Controls.Add(CreateSingleEditorRow("编辑组件", _fontTargetComboBox), 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "系统字体库",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(71, 85, 105),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        var searchHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        searchHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        searchHost.Controls.Add(_fontSearchTextBox, 0, 0);
        searchHost.Controls.Add(_openFontsFolderButton, 1, 0);
        layout.Controls.Add(searchHost, 0, 2);

        layout.Controls.Add(_fontListBox, 0, 3);
        layout.Controls.Add(_fontPreviewPanel, 0, 4);
        _fontPanel.Controls.Add(layout);
    }

    private void LoadValues()
    {
        _isLoadingValues = true;
        _backgroundColorTextBox.Text = _settings.BackgroundColor;
        _opacityTrackBar.Value = (int)Math.Round(_settings.BackgroundOpacity * 100);
        _widthUpDown.Value = (decimal)_settings.BackgroundWidth;
        _heightUpDown.Value = (decimal)_settings.BackgroundHeight;
        _cornerRadiusTrackBar.Value = (int)Math.Round(_settings.CornerRadius);

        if (_allFontNames.Count == 0)
        {
            _allFontNames.AddRange(FontFamily.Families
                .Select(static family => family.Name)
                .OrderBy(static name => name)
                .ToList());
        }

        SyncComponentSelectors();
        LoadComponentValues();
        _isLoadingValues = false;
    }

    private void LoadComponentValues()
    {
        _isLoadingValues = true;

        var element = _settings.GetElementSettings(_selectedItem);
        var colors = DrawingHelpers.EnsureGradientColors(element.GradientColors);
        var isTime = _selectedItem == ClockDisplayItem.Time;
        var resolvedTimeFormat = ClockFormatHelpers.NormalizeTimeFormat(_settings.TimeElement.CustomFormat, _settings.DisplayFormat);

        _visibleCheckBox.Checked = element.IsVisible;
        _sizeScaleUpDown.Value = CoerceNumericValue(_sizeScaleUpDown, element.SizeScale);
        _timeFormatComboBox.SelectedIndex = isTime ? GetTimeFormatPresetIndex(resolvedTimeFormat) : -1;
        _timeFormatComboBox.Enabled = isTime;
        _customFormatTextBox.Text = isTime ? resolvedTimeFormat : element.CustomFormat;
        _customFormatTextBox.Enabled = !isTime || _timeFormatComboBox.SelectedIndex == 2;
        _customFormatTextBox.PlaceholderText = GetDefaultFormat(_selectedItem);
        _displayHintLabel.Text = GetDisplayHint(_selectedItem);

        _offsetXUpDown.Value = CoerceNumericValue(_offsetXUpDown, element.OffsetX);
        _positionHintLabel.Text = "偏移量以默认布局为基准，单位为像素。";

        _fillModeComboBox.SelectedIndex = element.FillMode == TextFillMode.Solid ? 0 : 1;
        _primaryColorTextBox.Text = colors[0];
        _secondaryColorTextBox.Text = colors[1];
        _gradientDirectionComboBox.SelectedIndex = element.GradientDirection == GradientDirection.LeftToRight ? 0 : 1;
        UpdateGradientControls();
        _colorPreviewPanel.Invalidate();

        ApplyFontFilter();
        _fontPreviewPanel.Invalidate();
        _isLoadingValues = false;
    }

    private void ApplyFontFilter()
    {
        var element = _settings.GetElementSettings(_selectedItem);
        var selectedFont = element.FontFamilyName;
        var keyword = _fontSearchTextBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allFontNames
            : _allFontNames.Where(name => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        _isLoadingValues = true;
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

        _isLoadingValues = false;
    }

    private void OnComponentTargetChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues || sender is not ComboBox comboBox || comboBox.SelectedIndex < 0)
        {
            return;
        }

        _selectedItem = (ClockDisplayItem)comboBox.SelectedIndex;
        SyncComponentSelectors();
        LoadComponentValues();
    }

    private void SyncComponentSelectors()
    {
        var index = (int)_selectedItem;
        _displayTargetComboBox.SelectedIndex = index;
        _positionTargetComboBox.SelectedIndex = index;
        _colorTargetComboBox.SelectedIndex = index;
        _fontTargetComboBox.SelectedIndex = index;
    }

    private void OnVisibleCheckBoxChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues)
        {
            return;
        }

        var element = _settings.GetElementSettings(_selectedItem);
        element.IsVisible = _visibleCheckBox.Checked;
        _settings.ShowDate = _settings.DateElement.IsVisible;
        _settings.ShowWeekday = _settings.WeekdayElement.IsVisible;
        NotifyChanged();
    }

    private void OnSizeScaleChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues)
        {
            return;
        }

        _settings.GetElementSettings(_selectedItem).SizeScale = (double)_sizeScaleUpDown.Value;
        NotifyChanged();
    }
    private void OnTimeFormatChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues || _selectedItem != ClockDisplayItem.Time || _timeFormatComboBox.SelectedIndex < 0)
        {
            return;
        }

        var presetFormat = GetTimeFormatPreset(_timeFormatComboBox.SelectedIndex);
        if (presetFormat is not null)
        {
            _settings.TimeElement.CustomFormat = presetFormat;
            _settings.DisplayFormat = ClockFormatHelpers.InferDisplayFormat(presetFormat);

            _isLoadingValues = true;
            _customFormatTextBox.Text = presetFormat;
            _customFormatTextBox.Enabled = false;
            _isLoadingValues = false;
        }
        else
        {
            _customFormatTextBox.Enabled = true;
            _customFormatTextBox.Focus();
            _customFormatTextBox.SelectionStart = _customFormatTextBox.TextLength;
        }

        NotifyChanged();
    }

    private void OnCustomFormatChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues)
        {
            return;
        }

        var element = _settings.GetElementSettings(_selectedItem);
        element.CustomFormat = _customFormatTextBox.Text.Trim();

        if (_selectedItem == ClockDisplayItem.Time)
        {
            _settings.DisplayFormat = ClockFormatHelpers.InferDisplayFormat(element.CustomFormat);

            _isLoadingValues = true;
            _timeFormatComboBox.SelectedIndex = GetTimeFormatPresetIndex(element.CustomFormat);
            _customFormatTextBox.Enabled = _timeFormatComboBox.SelectedIndex == 2;
            _isLoadingValues = false;
        }

        NotifyChanged();
    }

    private void OnOffsetValueChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues)
        {
            return;
        }

        var element = _settings.GetElementSettings(_selectedItem);
        element.OffsetX = (double)_offsetXUpDown.Value;
        element.OffsetY = (double)_offsetYUpDown.Value;
        NotifyChanged();
    }

    private void OnFillModeChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues || _fillModeComboBox.SelectedIndex < 0)
        {
            return;
        }

        var element = _settings.GetElementSettings(_selectedItem);
        element.FillMode = _fillModeComboBox.SelectedIndex == 0 ? TextFillMode.Solid : TextFillMode.Gradient;
        UpdateGradientControls();
        _colorPreviewPanel.Invalidate();
        NotifyChanged();
    }

    private void OnGradientDirectionChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues || _gradientDirectionComboBox.SelectedIndex < 0)
        {
            return;
        }

        var element = _settings.GetElementSettings(_selectedItem);
        element.GradientDirection = _gradientDirectionComboBox.SelectedIndex == 0
            ? GradientDirection.LeftToRight
            : GradientDirection.TopToBottom;
        _colorPreviewPanel.Invalidate();
        NotifyChanged();
    }

    private void OnFontSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isLoadingValues || _fontListBox.SelectedItem is not string fontName)
        {
            return;
        }

        _settings.GetElementSettings(_selectedItem).FontFamilyName = fontName;
        _fontPreviewPanel.Invalidate();
        NotifyChanged();
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
        NotifyChanged();
    }

    private void PickColor(int index)
    {
        var element = _settings.GetElementSettings(_selectedItem);
        var colors = DrawingHelpers.EnsureGradientColors(element.GradientColors).ToArray();

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
        element.GradientColors = colors.ToList();
        LoadComponentValues();
        NotifyChanged();
    }

    private void UpdateGradientControls()
    {
        var element = _settings.GetElementSettings(_selectedItem);
        var isGradient = element.FillMode == TextFillMode.Gradient;
        _secondaryColorTextBox.Enabled = isGradient;
        _secondaryColorButton.Enabled = isGradient;
        _gradientDirectionComboBox.Enabled = isGradient;
    }

    private void OnColorPreviewPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Color.FromArgb(226, 232, 240));

        var element = _settings.GetElementSettings(_selectedItem);
        using var font = DrawingHelpers.CreateDisplayFont(element.FontFamilyName, GetPreviewFontSize(_selectedItem) * (float)element.SizeScale, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var rect = _colorPreviewPanel.ClientRectangle;
        using var brush = CreateElementBrush(rect, element);
        e.Graphics.DrawString(GetPreviewText(_selectedItem), font, brush, rect, format);
    }

    private void OnFontPreviewPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(226, 232, 240));
        var element = _settings.GetElementSettings(_selectedItem);
        using var font = DrawingHelpers.CreateDisplayFont(element.FontFamilyName, GetPreviewFontSize(_selectedItem) * (float)element.SizeScale, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(GetPreviewText(_selectedItem), font, brush, _fontPreviewPanel.ClientRectangle, format);
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

    private void NotifyChanged()
    {
        _settings.Normalize();
        _colorPreviewPanel.Invalidate();
        _fontPreviewPanel.Invalidate();
        _onChanged();
    }

    private static ComboBox CreateComponentComboBox()
    {
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        comboBox.Items.AddRange(new object[] { "鏃堕棿", "鏃ユ湡", "鏄熸湡" });
        comboBox.SelectedIndex = 0;
        return comboBox;
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

    private static TableLayoutPanel CreateEditorTable()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        return layout;
    }

    private static Control CreateSingleEditorRow(string labelText, Control control)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(control, 1, 0);
        return layout;
    }

    private static void AddRow(TableLayoutPanel layout, int rowIndex, string labelText, Control mainControl, Button? button, EventHandler? buttonHandler, int columnSpan = 1)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.Controls.Add(new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, rowIndex);

        layout.Controls.Add(mainControl, 1, rowIndex);
        layout.SetColumnSpan(mainControl, button is null ? 2 : 1);

        if (button is null)
        {
            return;
        }

        button.Click += buttonHandler;
        layout.Controls.Add(button, 2, rowIndex);
    }

    private static decimal CoerceNumericValue(NumericUpDown input, double value)
    {
        var safeValue = double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
        var decimalValue = (decimal)safeValue;
        return Math.Min(input.Maximum, Math.Max(input.Minimum, decimalValue));
    }

    private string GetDisplayHint(ClockDisplayItem item)
    {
        if (item == ClockDisplayItem.Time)
        {
            return "时间支持 .NET 日期时间格式，例如 HH:mm、HH:mm:ss，并支持单独调整大小。";
        }

        return item switch
        {
            ClockDisplayItem.Time => "时间组件支持格式与大小单独调整。",
            ClockDisplayItem.Date => "日期支持 .NET 日期格式，也支持单独调整大小。",
            ClockDisplayItem.Weekday => "星期支持 .NET 日期格式，也支持单独调整大小。",
            _ => string.Empty
        };
    }

    private static string GetDefaultFormat(ClockDisplayItem item)
    {
        if (item == ClockDisplayItem.Time)
        {
            return "HH:mm";
        }

        return item switch
        {
            ClockDisplayItem.Date => "yyyy-MM-dd",
            ClockDisplayItem.Weekday => "dddd",
            _ => string.Empty
        };
    }

    private static float GetPreviewFontSize(ClockDisplayItem item)
    {
        return item == ClockDisplayItem.Time ? 42f : 28f;
    }

    private string GetPreviewText(ClockDisplayItem item)
    {
        var previewTime = new DateTime(2026, 4, 4, 12, 34, 56);
        var element = _settings.GetElementSettings(item);

        return item switch
        {
            ClockDisplayItem.Time => ClockFormatHelpers.FormatDateTime(
                previewTime,
                element.CustomFormat,
                ClockFormatHelpers.GetFallbackTimeFormat(_settings.DisplayFormat),
                CultureInfo.CurrentCulture),
            ClockDisplayItem.Date => FormatDateTime(previewTime, element.CustomFormat, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ClockDisplayItem.Weekday => FormatDateTime(previewTime, element.CustomFormat, "dddd", ChineseCulture),
            _ => "12:34"
        };
    }

    private static int GetTimeFormatPresetIndex(string? format)
    {
        return format?.Trim() switch
        {
            "HH:mm" => 0,
            "HH:mm:ss" => 1,
            _ => 2
        };
    }

    private static string? GetTimeFormatPreset(int selectedIndex)
    {
        return selectedIndex switch
        {
            0 => "HH:mm",
            1 => "HH:mm:ss",
            _ => null
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

    private static Brush CreateElementBrush(Rectangle bounds, ClockElementSettings element)
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
}




