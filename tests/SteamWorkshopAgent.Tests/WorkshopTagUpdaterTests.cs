using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class WorkshopTagUpdaterTests : IDisposable
{
    private readonly string? previousNativeLibrary;
    private readonly string nativeLibraryPath;

    public WorkshopTagUpdaterTests()
    {
        previousNativeLibrary = Environment.GetEnvironmentVariable("STEAMWORKS_NATIVE_LIB");
        var root = Path.Combine(Path.GetTempPath(), "steam-workshop-agent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        nativeLibraryPath = Path.Combine(root, "libsteam_api.dylib");
        File.WriteAllBytes(nativeLibraryPath, [0]);
        Environment.SetEnvironmentVariable("STEAMWORKS_NATIVE_LIB", nativeLibraryPath);
    }

    [Fact]
    public async Task CreatePlan_Uses_Default_Mod_Tags_For_Mod_Path()
    {
        using var fixture = TestModRepo.CreateDeployed(includePublishedFileId: true);
        var updater = CreateUpdater();

        var plan = await updater.CreatePlanAsync(fixture.Root, []);

        Assert.Equal((ulong)123456789, plan.PublishedFileId);
        Assert.Equal(["Mod", "1.6"], plan.Tags);
        Assert.Equal(nativeLibraryPath, plan.NativeLibraryPath);
        Assert.DoesNotContain(plan.ValidationIssues, issue => issue.Severity == "error");
    }

    [Fact]
    public async Task CreatePlan_Accepts_Explicit_Id_And_Normalizes_Tags()
    {
        var updater = CreateUpdater();

        var plan = await updater.CreatePlanAsync("3727949765", [" Mod ", "1.6", "mod", ""]);

        Assert.Equal((ulong)3727949765, plan.PublishedFileId);
        Assert.Equal(["Mod", "1.6"], plan.Tags);
        Assert.Equal(nativeLibraryPath, plan.NativeLibraryPath);
        Assert.DoesNotContain(plan.ValidationIssues, issue => issue.Severity == "error");
    }

    [Fact]
    public async Task CreatePlan_Requires_Published_File_Id_For_Mod_Path()
    {
        using var fixture = TestModRepo.CreateDeployed(includePublishedFileId: false);
        var updater = CreateUpdater();

        var plan = await updater.CreatePlanAsync(fixture.Root, []);

        Assert.Contains(plan.ValidationIssues, issue => issue.Code == "missing_published_file_id" && issue.Severity == "error");
    }

    [Fact]
    public async Task SetTags_DryRun_Returns_NonMutating_Result_For_Explicit_Id()
    {
        var updater = CreateUpdater();

        var result = await updater.SetTagsAsync(3727949765, ["Mod", "1.6"], confirm: false);

        Assert.False(result.Success);
        Assert.Equal((ulong)3727949765, result.PublishedFileId);
        Assert.Equal(["Mod", "1.6"], result.Tags);
        Assert.False(result.SteamInitialized);
        Assert.Contains("Dry run", result.Message);
    }

    [Fact]
    public async Task CreateChangeNotePlan_Accepts_Explicit_Id_Without_Tags()
    {
        var updater = CreateUpdater();

        var plan = await updater.CreateChangeNotePlanAsync(
            "3727949765",
            "This release fixes startup and makes reconnecting more reliable.");

        Assert.Equal((ulong)3727949765, plan.PublishedFileId);
        Assert.Empty(plan.Tags);
        Assert.Equal("This release fixes startup and makes reconnecting more reliable.", plan.ChangeNote);
        Assert.Equal(nativeLibraryPath, plan.NativeLibraryPath);
        Assert.DoesNotContain(plan.ValidationIssues, issue => issue.Code == "missing_tags");
        Assert.DoesNotContain(plan.ValidationIssues, issue => issue.Severity == "error");
    }

    [Fact]
    public async Task CreateChangeNotePlan_Requires_ChangeNote()
    {
        var updater = CreateUpdater();

        var plan = await updater.CreateChangeNotePlanAsync("3727949765", " ");

        Assert.Contains(plan.ValidationIssues, issue => issue.Code == "missing_change_note" && issue.Severity == "error");
    }

    [Fact]
    public async Task SetChangeNote_DryRun_Returns_NonMutating_Result_For_Explicit_Id()
    {
        var updater = CreateUpdater();

        var result = await updater.SetChangeNoteAsync(
            3727949765,
            "This release fixes startup and makes reconnecting more reliable.",
            confirm: false);

        Assert.False(result.Success);
        Assert.Equal((ulong)3727949765, result.PublishedFileId);
        Assert.Empty(result.Tags);
        Assert.Equal("This release fixes startup and makes reconnecting more reliable.", result.ChangeNote);
        Assert.False(result.SteamInitialized);
        Assert.Contains("Dry run", result.Message);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("STEAMWORKS_NATIVE_LIB", previousNativeLibrary);
        var root = Path.GetDirectoryName(nativeLibraryPath);
        if (root != null && Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static WorkshopTagUpdater CreateUpdater()
    {
        var processRunner = new ProcessRunner();
        return new WorkshopTagUpdater(
            new SteamEnvironment(processRunner),
            new ModInspector(processRunner),
            processRunner);
    }
}
