using DesktopClock.Native;

namespace DesktopClock.Services;

public sealed class DesktopLayerService
{
    private static readonly IntPtr HwndBottom = new(1);

    public bool TryAttachToDesktop(IntPtr windowHandle)
    {
        var workerWindow = FindWorkerWindow();

        if (workerWindow == IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(
                windowHandle,
                HwndBottom,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
            return false;
        }

        NativeMethods.SetParent(windowHandle, workerWindow);
        NativeMethods.SetWindowPos(
            windowHandle,
            HwndBottom,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);

        return true;
    }

    private static IntPtr FindWorkerWindow()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            NativeMethods.SendMessageTimeout(
                progman,
                0x052C,
                IntPtr.Zero,
                IntPtr.Zero,
                0,
                1000,
                out _);
        }

        var workerWindow = IntPtr.Zero;

        NativeMethods.EnumWindows((topLevelWindow, _) =>
        {
            var shellView = NativeMethods.FindWindowEx(topLevelWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero)
            {
                return true;
            }

            workerWindow = NativeMethods.FindWindowEx(IntPtr.Zero, topLevelWindow, "WorkerW", null);
            return workerWindow == IntPtr.Zero;
        }, IntPtr.Zero);

        return workerWindow;
    }
}
