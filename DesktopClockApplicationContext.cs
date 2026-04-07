using System.Windows.Forms;
using DesktopClock.Dialogs;
using DesktopClock.Forms;
using DesktopClock.Helpers;
using DesktopClock.Models;
using DesktopClock.Services;

namespace DesktopClock;

internal sealed class DesktopClockApplicationContext : ApplicationContext
{
    private readonly SettingsService _settingsService = new();
    private readonly StartupRegistrationService _startupRegistrationService = new();
    private readonly ClockUpdateScheduler _clockUpdateScheduler = new();
    private readonly DesktopLayerService _desktopLayerService = new();
    private readonly MemoryTrimService _memoryTrimService = new();
    private readonly System.Windows.Forms.Timer _settingsSaveTimer = new();
    private readonly System.Windows.Forms.Timer _memoryTrimTimer = new();

    private readonly ClockForm _clockForm;
    private readonly TrayIconService _trayIconService;
    private SettingsForm? _settingsForm;
    private ClockSettings _settings;
    private bool _isExiting;

    public DesktopClockApplicationContext()
    {
        _settings = _settingsService.Load();

        _settingsSaveTimer.Interval = 400;
        _settingsSaveTimer.Tick += OnSettingsSaveTimerTick;

        _memoryTrimTimer.Interval = 12000;
        _memoryTrimTimer.Tick += OnMemoryTrimTimerTick;

        _clockForm = new ClockForm();
        _clockForm.Initialize(_settings);
        _clockForm.TransformCommitted += OnWindowTransformCommitted;
        _clockForm.FormClosed += OnClockFormClosed;
        _clockForm.Show();
        _clockForm.AttachToDesktop(_desktopLayerService);

        _trayIconService = new TrayIconService(
            onEditModeChanged: SetEditMode,
            onSettingsRequested: OpenSettings,
            onLaunchAtStartupChanged: SetLaunchAtStartup,
            onExitRequested: ExitApplication);

        _trayIconService.UpdateState(_settings);
        _clockUpdateScheduler.TimeTextChanged += OnTimeTextChanged;
        _clockUpdateScheduler.Start(_settings.TimeElement.CustomFormat);

        ApplySettings(persistNow: false, refreshSchedule: false, scheduleSave: false);
        EnsureLaunchAtStartup();
        _memoryTrimTimer.Start();
        MainForm = _clockForm;
    }

    protected override void ExitThreadCore()
    {
        _settingsSaveTimer.Stop();
        _memoryTrimTimer.Stop();
        SaveSettings();
        _trayIconService.Dispose();
        _clockUpdateScheduler.Dispose();
        DrawingHelpers.DisposeCachedResources();
        base.ExitThreadCore();
    }

    private void ApplySettings(bool persistNow, bool refreshSchedule, bool scheduleSave)
    {
        _settings.Normalize();
        _clockForm.ApplySettings(_settings);
        _trayIconService.UpdateState(_settings);

        if (refreshSchedule)
        {
            _clockUpdateScheduler.Start(_settings.TimeElement.CustomFormat);
        }

        EnsureLaunchAtStartup();

        if (persistNow)
        {
            SaveSettings();
            ScheduleMemoryTrim();
        }
        else if (scheduleSave)
        {
            ScheduleSave();
            ScheduleMemoryTrim();
        }
    }

    private void SetEditMode(bool isEnabled)
    {
        _settings.IsEditMode = isEnabled;
        ApplySettings(persistNow: true, refreshSchedule: false, scheduleSave: false);
    }

    private void SetLaunchAtStartup(bool isEnabled)
    {
        _settings.LaunchAtStartup = isEnabled;
        ApplySettings(persistNow: true, refreshSchedule: false, scheduleSave: false);
    }

    private void OpenSettings()
    {
        ShowSettings(SettingsSection.Background);
    }

    private void ShowSettings(SettingsSection section)
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings, OnEditorSettingsChanged);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        }

        _settingsForm.ShowSection(section);
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnEditorSettingsChanged()
    {
        ApplySettings(persistNow: false, refreshSchedule: true, scheduleSave: true);
    }

    private void OnTimeTextChanged(string timeText)
    {
        if (_clockForm.IsDisposed || !_clockForm.IsHandleCreated)
        {
            return;
        }

        _clockForm.BeginInvoke(() => _clockForm.UpdateDisplayedTime(timeText));
    }

    private void OnWindowTransformCommitted(object? sender, EventArgs e)
    {
        _settings.WindowLeft = _clockForm.Left;
        _settings.WindowTop = _clockForm.Top;
        _settings.Scale = _clockForm.CurrentScale;
        ApplySettings(persistNow: true, refreshSchedule: false, scheduleSave: false);
        _clockForm.AttachToDesktop(_desktopLayerService);
    }

    private void OnClockFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (!_isExiting)
        {
            ExitApplication();
        }
    }

    private void OnSettingsSaveTimerTick(object? sender, EventArgs e)
    {
        _settingsSaveTimer.Stop();
        SaveSettings();
    }

    private void ScheduleSave()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void SaveSettings()
    {
        _settingsSaveTimer.Stop();
        _settingsService.Save(_settings);
    }

    private void OnMemoryTrimTimerTick(object? sender, EventArgs e)
    {
        TrimMemory();
    }

    private void ScheduleMemoryTrim()
    {
        _memoryTrimTimer.Stop();
        _memoryTrimTimer.Start();
    }

    private void TrimMemory()
    {
        _memoryTrimTimer.Stop();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _memoryTrimService.TrimCurrentProcess();
        _memoryTrimTimer.Start();
    }

    private void EnsureLaunchAtStartup()
    {
        _startupRegistrationService.SetEnabled(_settings.LaunchAtStartup);
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _clockForm.Close();
        ExitThread();
    }
}
