using DesktopClock.Models;

namespace DesktopClock.Services;

public sealed class ClockUpdateScheduler : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public event Action<string>? TimeTextChanged;

    public void Start(ClockDisplayFormat displayFormat)
    {
        lock (_gate)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _ = RunAsync(displayFormat, _cancellationTokenSource.Token);
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

    private async Task RunAsync(ClockDisplayFormat displayFormat, CancellationToken cancellationToken)
    {
        var lastValue = string.Empty;

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextValue = displayFormat == ClockDisplayFormat.HoursMinutes
                ? now.ToString("HH:mm")
                : now.ToString("HH:mm:ss");

            if (!string.Equals(lastValue, nextValue, StringComparison.Ordinal))
            {
                lastValue = nextValue;
                TimeTextChanged?.Invoke(nextValue);
            }

            var nextTick = displayFormat == ClockDisplayFormat.HoursMinutes
                ? now.AddMinutes(1)
                : now.AddSeconds(1);

            nextTick = displayFormat == ClockDisplayFormat.HoursMinutes
                ? new DateTime(nextTick.Year, nextTick.Month, nextTick.Day, nextTick.Hour, nextTick.Minute, 0, now.Kind)
                : new DateTime(nextTick.Year, nextTick.Month, nextTick.Day, nextTick.Hour, nextTick.Minute, nextTick.Second, now.Kind);

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
