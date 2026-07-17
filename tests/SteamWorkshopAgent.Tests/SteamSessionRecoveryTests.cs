using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class SteamSessionRecoveryTests
{
    [Fact]
    public void NotLoggedOnMessage_Explains_SteamCmd_Session_Replacement_Recovery()
    {
        var message = SteamSessionRecovery.NotLoggedOnMessage("The initialized desktop Steam session");

        Assert.Contains("SteamCMD", message);
        Assert.Contains("replaced the desktop session", message);
        Assert.Contains("Fully quit and reopen Steam", message);
        Assert.Contains("probe again before", message);
        Assert.EndsWith("No submission was started.", message);
    }
}
