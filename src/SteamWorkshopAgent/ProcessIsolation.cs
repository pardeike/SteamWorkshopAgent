using System.Runtime.InteropServices;

namespace SteamWorkshopAgent;

internal static class ProcessIsolation
{
    public static bool TryDetachFromControllingTerminal(out string message)
    {
        if (OperatingSystem.IsWindows())
        {
            message = "Windows child process isolation is handled by ProcessStartInfo.";
            return true;
        }

        var sessionId = setsid();
        if (sessionId >= 0)
        {
            message = $"Created detached process session {sessionId}.";
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        message = $"setsid failed with errno {error}.";
        return false;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int setsid();
}
