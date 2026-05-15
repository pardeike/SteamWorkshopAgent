using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SteamWorkshopAgent;

[McpServerToolType]
public static class SteamTools
{
    [McpServerTool, Description("Inspect local SteamCMD/RimWorld Workshop upload prerequisites without publishing anything.")]
    public static Task<string> SteamStatus(
        [Description("When true, run `steamcmd +quit` to validate the SteamCMD executable. Defaults to false.")]
        bool runSteamCmdQuit = false)
    {
        return ToolJson.TryAsync(async () =>
        {
            var environment = ServiceLocator.Get<SteamEnvironment>();
            return await environment.GetStatusAsync(runSteamCmdQuit);
        });
    }
}

[McpServerToolType]
public static class RimWorldModTools
{
    [McpServerTool, Description("Inspect RimWorld mod repository metadata used for Steam Workshop publishing.")]
    public static Task<string> RimWorldModInspect(
        [Description("Absolute path to the RimWorld mod repository.")]
        string repoPath)
    {
        return ToolJson.TryAsync(async () =>
        {
            var inspector = ServiceLocator.Get<ModInspector>();
            return await inspector.InspectAsync(repoPath);
        });
    }
}

[McpServerToolType]
public static class WorkshopTools
{
    [McpServerTool, Description("Create a dry-run Steam Workshop release plan and VDF content from a GitHub release.")]
    public static Task<string> WorkshopReleasePlan(
        [Description("Absolute path to the RimWorld mod repository.")]
        string repoPath,
        [Description("GitHub release tag, e.g. v3.6.2.0.")]
        string tag,
        [Description("When true, include About/About.xml description in the VDF. Defaults to false to preserve the existing Workshop description on updates.")]
        bool updateDescription = false)
    {
        return ToolJson.TryAsync(async () =>
        {
            var planner = ServiceLocator.Get<WorkshopPlanner>();
            return await planner.CreateReleasePlanAsync(repoPath, tag, updateDescription);
        });
    }

    [McpServerTool, Description("Publish a GitHub release build to Steam Workshop using SteamCMD. Requires confirm=true and a Steam username or STEAMCMD_USER.")]
    public static Task<string> WorkshopPublishRelease(
        [Description("Absolute path to the RimWorld mod repository.")]
        string repoPath,
        [Description("GitHub release tag, e.g. v3.6.2.0.")]
        string tag,
        [Description("Must be true to run the release build and SteamCMD upload. False returns the dry-run plan.")]
        bool confirm = false,
        [Description("Steam username for `steamcmd +login`. If omitted, STEAMCMD_USER is used. Passwords are never accepted or stored.")]
        string? steamUser = null,
        [Description("When true, update the main Workshop description from About/About.xml. Defaults to false.")]
        bool updateDescription = false)
    {
        return ToolJson.TryAsync(async () =>
        {
            var publisher = ServiceLocator.Get<WorkshopPublisher>();
            return await publisher.PublishReleaseAsync(repoPath, tag, confirm, updateDescription, steamUser);
        });
    }
}
