using DesktopClock.Native;

namespace DesktopClock.Services;

public sealed class MemoryTrimService
{
    public void TrimCurrentProcess()
    {
        var processHandle = Environment.ProcessId;
        NativeMethods.TrimWorkingSet(processHandle);
    }
}
