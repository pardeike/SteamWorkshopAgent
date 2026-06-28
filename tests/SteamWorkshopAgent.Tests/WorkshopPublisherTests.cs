using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class WorkshopPublisherTests
{
    [Fact]
    public void CreateBuildReleaseArguments_Disables_BridgeTools_For_Release_Payloads()
    {
        var arguments = WorkshopPublisher.CreateBuildReleaseArguments(
            "/repo/Source/TestMod.csproj",
            "/tmp/stage");

        Assert.Equal(
            [
                "build",
                "/repo/Source/TestMod.csproj",
                "-c",
                "Release",
                "-p:RIMWORLD_MOD_DIR=/tmp/stage",
                "-p:BuildBridgeTools=false"
            ],
            arguments);
    }
}
