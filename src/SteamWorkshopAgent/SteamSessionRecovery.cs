namespace SteamWorkshopAgent;

internal static class SteamSessionRecovery
{
    public const string DesktopSessionOffline = "desktop-session-offline";
    public const string RestartSteam = "restart-steam";

    public static string NotLoggedOnMessage(string subject)
    {
        return $"{subject} has no live connection to the Steam servers. "
            + "If Steam is visibly open, another login—especially SteamCMD using the same account—may have replaced the desktop session. "
            + "Fully quit and reopen Steam, wait for it to reconnect, and run the desktop session probe again before starting RimWorld or selecting SteamCMD fallback. "
            + "No submission was started.";
    }
}
