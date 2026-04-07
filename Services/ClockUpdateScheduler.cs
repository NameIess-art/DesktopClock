using System.Globalization;
using DesktopClock.Helpers;
using DesktopClock.Models;

namespace DesktopClock.Services;

public sealed class ClockUpdateScheduler : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public event Action<string>? TimeTextChanged;

    public void Start(string? timeFormat)
    {
        var normalizedFormat = ClockFormatHelpers.NormalizeTimeFormat(timeFormat, ClockDisplayFormat.HoursMinutes);

        lock (_gate)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _ = RunAsync(normalizedFormat, _cancellationTokenSource.Token);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async Task RunAsync(string timeFormat, CancellationToken cancellationToken)
    {
        var lastValue = string.Empty;
        var updatesEachSecond = ClockFormatHelpers.UsesSecondPrecision(timeFormat);

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextValue = ClockFormatHelpers.FormatDateTime(
                now,
                timeFormat,
                ClockFormatHelpers.GetFallbackTimeFormat(ClockDisplayFormat.HoursMinutes),
                CultureInfo.CurrentCulture);

            if (!string.Equals(lastValue, nextValue, StringComparison.Ordinal))
            {
                lastValue = nextValue;
                TimeTextChanged?.Invoke(nextValue);
            }

            var nextTick = updatesEachSecond
                ? now.AddSeconds(1)
                : now.AddMinutes(1);

            nextTick = updatesEachSecond
                ? new DateTime(nextTick.Year, nextTick.Month, nextTick.Day, nextTick.Hour, nextTick.Minute, nextTick.Second, now.Kind)
                : new DateTime(nextTick.Year, nextTick.Month, nextTick.Day, nextTick.Hour, nextTick.Minute, 0, now.Kind);

            var delay = nextTick - DateTime.Now;
            if (delay < TimeSpan.FromMilliseconds(50))
            {
                delay = TimeSpan.FromMilliseconds(50);
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
