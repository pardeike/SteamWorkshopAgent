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

    [Fact]
    public void CreateSteamChangeNote_Prefixes_Mod_Name_And_Short_Version()
    {
        var mod = TestModRepo.SampleInspection() with { ModVersion = "1.2.3.0" };
        var release = new GitHubReleaseInfo(
            TagName: "v1.2.3.0",
            Name: "Test Mod 1.2.3",
            Body: "Release body",
            Url: "https://github.com/example/TestMod/releases/tag/v1.2.3.0",
            ChangeNote: "Release body\n\nGitHub release: https://github.com/example/TestMod/releases/tag/v1.2.3.0");

        var changeNote = WorkshopPlanner.CreateSteamChangeNote(mod, release);

        Assert.Equal(
            "Test Mod v1.2.3\n\nRelease body\n\nGitHub release: https://github.com/example/TestMod/releases/tag/v1.2.3.0",
            changeNote);
    }

    [Fact]
    public void CreateSteamChangeNote_Does_Not_Duplicate_Existing_Header()
    {
        var mod = TestModRepo.SampleInspection() with { ModVersion = "1.2.3.0" };
        var release = new GitHubReleaseInfo(
            TagName: "v1.2.3.0",
            Name: "Test Mod 1.2.3",
            Body: "Test Mod v1.2.3\n\nRelease body",
            Url: "https://github.com/example/TestMod/releases/tag/v1.2.3.0",
            ChangeNote: "Test Mod v1.2.3\n\nRelease body\n\nGitHub release: https://github.com/example/TestMod/releases/tag/v1.2.3.0");

        var changeNote = WorkshopPlanner.CreateSteamChangeNote(mod, release);

        Assert.Equal(release.ChangeNote, changeNote);
    }
}
