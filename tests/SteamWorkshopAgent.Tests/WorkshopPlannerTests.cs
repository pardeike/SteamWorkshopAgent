using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class WorkshopPlannerTests
{
    [Fact]
    public async Task CreateNewModPlan_Uses_Deployed_Mod_Folder_As_ContentFolder()
    {
        using var fixture = TestModRepo.CreateDeployed();
        var planner = CreatePlanner();

        var plan = await planner.CreateNewModPlanAsync(fixture.Root);

        Assert.Equal(fixture.Root, plan.ContentFolder);
        Assert.Equal(Path.Combine(fixture.Root, "About", "Preview.png"), plan.PreviewFile);
        Assert.False(plan.Mod.HasBuildProject);
        Assert.DoesNotContain(plan.ValidationIssues, issue => issue.Severity == "error");
    }

    [Fact]
    public async Task CreateReleasePlan_Rejects_Deployed_Mod_Folder()
    {
        using var fixture = TestModRepo.CreateDeployed();
        var planner = CreatePlanner();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => planner.CreateReleasePlanAsync(fixture.Root, "v1.0.0"));

        Assert.Contains("requires a source repository", error.Message);
    }

    private static WorkshopPlanner CreatePlanner()
    {
        var processRunner = new ProcessRunner();
        return new WorkshopPlanner(
            new ModInspector(processRunner),
            new GitHubReleaseReader(processRunner));
    }
}
