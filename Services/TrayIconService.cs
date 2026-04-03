using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DesktopClock.Models;
using Microsoft.Win32;

namespace DesktopClock.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _editModeItem;
    private readonly ToolStripMenuItem _minutesFormatItem;
    private readonly ToolStripMenuItem _secondsFormatItem;
    private readonly ToolStripMenuItem _launchAtStartupItem;
    private readonly ThemeIconFactory _themeIconFactory = new();
    private readonly string _fallbackIconPath = Path.Combine(AppContext.BaseDirectory, "clock.ico");

    private Icon? _currentIcon;
    private bool _isUpdatingMenuState;

    public TrayIconService(
        Action<bool> onEditModeChanged,
        Action<ClockDisplayFormat> onDisplayFormatChanged,
        Action onSettingsRequested,
        Action<bool> onLaunchAtStartupChanged,
        Action onExitRequested)
    {
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "DesktopClock"
        };

        _editModeItem = new ToolStripMenuItem("编辑模式")
        {
            CheckOnClick = true
        };
        _editModeItem.CheckedChanged += (_, _) =>
        {
            if (!_isUpdatingMenuState)
            {
                onEditModeChanged(_editModeItem.Checked);
            }
        };

        _minutesFormatItem = new ToolStripMenuItem("HH:mm");
        _minutesFormatItem.Click += (_, _) => onDisplayFormatChanged(ClockDisplayFormat.HoursMinutes);

        _secondsFormatItem = new ToolStripMenuItem("HH:mm:ss");
        _secondsFormatItem.Click += (_, _) => onDisplayFormatChanged(ClockDisplayFormat.HoursMinutesSeconds);

        var displayFormatItem = new ToolStripMenuItem("显示格式");
        displayFormatItem.DropDownItems.Add(_minutesFormatItem);
        displayFormatItem.DropDownItems.Add(_secondsFormatItem);

        var settingsItem = new ToolStripMenuItem("设置...");
        settingsItem.Click += (_, _) => onSettingsRequested();

        _launchAtStartupItem = new ToolStripMenuItem("开机自启")
        {
            CheckOnClick = true
        };
        _launchAtStartupItem.CheckedChanged += (_, _) =>
        {
            if (!_isUpdatingMenuState)
            {
                onLaunchAtStartupChanged(_launchAtStartupItem.Checked);
            }
        };

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => onExitRequested();

        _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add(_editModeItem);
        _notifyIcon.ContextMenuStrip.Items.Add(displayFormatItem);
        _notifyIcon.ContextMenuStrip.Items.Add(settingsItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_launchAtStartupItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(exitItem);

        RefreshIcon();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void UpdateState(ClockSettings settings)
    {
        _isUpdatingMenuState = true;
        _editModeItem.Checked = settings.IsEditMode;
        _launchAtStartupItem.Checked = settings.LaunchAtStartup;
        _minutesFormatItem.Checked = settings.DisplayFormat == ClockDisplayFormat.HoursMinutes;
        _secondsFormatItem.Checked = settings.DisplayFormat == ClockDisplayFormat.HoursMinutesSeconds;
        _isUpdatingMenuState = false;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        var color = SystemThemeHelper.IsDarkMode() ? Color.White : Color.Black;
        var previous = _currentIcon;

        try
        {
            _currentIcon = _themeIconFactory.CreateIcon(color);
        }
        catch
        {
            _currentIcon = File.Exists(_fallbackIconPath) ? new Icon(_fallbackIconPath) : SystemIcons.Application;
        }

        _notifyIcon.Icon = _currentIcon;
        previous?.Dispose();
    }
}
