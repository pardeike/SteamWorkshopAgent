using System.Text.Json;

namespace SteamWorkshopAgent;

public sealed class WorkshopPublisher(
    ProcessRunner processRunner,
    SteamEnvironment steamEnvironment,
    WorkshopPlanner workshopPlanner,
    WorkshopTagUpdater tagUpdater)
{
    public async Task<object> PublishReleaseAsync(
        string repoPath,
        string tag,
        bool confirm,
        bool updateDescription = false,
        string? steamUser = null)
    {
        if (!confirm)
            return await workshopPlanner.CreateReleasePlanAsync(repoPath, tag, updateDescription);

        var initialPlan = await workshopPlanner.CreateReleasePlanAsync(repoPath, tag, updateDescription);
        Directory.CreateDirectory(initialPlan.RunDirectory);
        Directory.CreateDirectory(initialPlan.StagingRoot);

        var beforeStatus = await processRunner.RunAsync("git", ["-C", initialPlan.Mod.RepoPath, "status", "--short"], timeout: TimeSpan.FromSeconds(10));
        if (beforeStatus.ExitCode == 0 && !string.IsNullOrWhiteSpace(beforeStatus.Stdout))
            throw new InvalidOperationException("Refusing to publish from a dirty worktree. Commit or stash local changes first.");

        await BuildReleaseAsync(initialPlan.Mod, initialPlan.StagingRoot);

        var finalPlan = await workshopPlanner.CreateReleasePlanAsync(
            repoPath,
            tag,
            updateDescription,
            initialPlan.RunDirectory,
            initialPlan.ContentFolder);

        Validation.ThrowIfErrors(finalPlan.ValidationIssues);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPlan.VdfPath)!);
        await File.WriteAllTextAsync(finalPlan.VdfPath, finalPlan.VdfContent);
        await File.WriteAllTextAsync(
            Path.Combine(finalPlan.RunDirectory, "plan.json"),
            JsonSerializer.Serialize(finalPlan, ToolJson.Options));

        var steamCmdPath = steamEnvironment.RequireSteamCmd();
        var resolvedSteamUser = steamEnvironment.RequireSteamUser(steamUser);
        var steamResult = await processRunner.RunAsync(
            steamCmdPath,
            ["+login", resolvedSteamUser, "+workshop_build_item", finalPlan.VdfPath, "+quit"],
            workingDirectory: finalPlan.RunDirectory,
            timeout: TimeSpan.FromMinutes(15));

        var logTails = steamEnvironment.GetWorkshopLogPaths()
            .Where(File.Exists)
            .Select(path => TailFile(path, 80))
            .ToList();
        var tagUpdate = steamResult.ExitCode == 0 && finalPlan.Mod.PublishedFileId is { } publishedFileId
            ? await tagUpdater.SetTagsAsync(publishedFileId, finalPlan.IntendedTags, confirm: true, changeNote: "Set Workshop tags")
            : null;

        return new PublishResult(
            steamResult.ExitCode == 0,
            steamResult.ExitCode,
            finalPlan.Mod.WorkshopUrl,
            finalPlan.RunDirectory,
            finalPlan.VdfPath,
            finalPlan.ContentFolder,
            Truncate(steamResult.Stdout, 12000),
            Truncate(steamResult.Stderr, 12000),
            logTails,
            tagUpdate,
            finalPlan);
    }

    public async Task<object> CreateNewModAsync(
        string repoPath,
        bool confirm,
        string? steamUser = null,
        string visibility = "private",
        string changeNote = "Initial upload")
    {
        if (!confirm)
            return await workshopPlanner.CreateNewModPlanAsync(repoPath, visibility, changeNote);

        var initialPlan = await workshopPlanner.CreateNewModPlanAsync(repoPath, visibility, changeNote);
        Validation.ThrowIfErrors(initialPlan.ValidationIssues);
        Directory.CreateDirectory(initialPlan.RunDirectory);
        Directory.CreateDirectory(initialPlan.StagingRoot);

        var finalPlan = initialPlan;
        if (initialPlan.Mod.HasBuildProject)
        {
            await BuildReleaseAsync(initialPlan.Mod, initialPlan.StagingRoot);

            finalPlan = await workshopPlanner.CreateNewModPlanAsync(
                repoPath,
                visibility,
                changeNote,
                initialPlan.RunDirectory,
                initialPlan.ContentFolder);
        }

        Validation.ThrowIfErrors(finalPlan.ValidationIssues);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPlan.VdfPath)!);
        await File.WriteAllTextAsync(finalPlan.VdfPath, finalPlan.VdfContent);
        await File.WriteAllTextAsync(
            Path.Combine(finalPlan.RunDirectory, "plan.json"),
            JsonSerializer.Serialize(finalPlan, ToolJson.Options));

        var steamCmdPath = steamEnvironment.RequireSteamCmd();
        var resolvedSteamUser = steamEnvironment.RequireSteamUser(steamUser);
        var steamResult = await processRunner.RunAsync(
            steamCmdPath,
            ["+login", resolvedSteamUser, "+workshop_build_item", finalPlan.VdfPath, "+quit"],
            workingDirectory: finalPlan.RunDirectory,
            timeout: TimeSpan.FromMinutes(15));

        var publishedFileId = TryReadPublishedFileId(finalPlan.VdfPath);
        var publishedFileIdPath = GetPublishedFileIdPath(finalPlan.Mod);
        var success = steamResult.ExitCode == 0 && publishedFileId.GetValueOrDefault() != 0;
        if (success)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(publishedFileIdPath)!);
            await File.WriteAllTextAsync(publishedFileIdPath, publishedFileId!.Value.ToString());
        }

        var logTails = steamEnvironment.GetWorkshopLogPaths()
            .Where(File.Exists)
            .Select(path => TailFile(path, 80))
            .ToList();
        var tagUpdate = success && publishedFileId is { } createdFileId
            ? await tagUpdater.SetTagsAsync(createdFileId, finalPlan.Tags, confirm: true, changeNote: "Set Workshop tags")
            : null;

        return new CreateNewModResult(
            success,
            steamResult.ExitCode,
            publishedFileId,
            publishedFileIdPath,
            publishedFileId.GetValueOrDefault() != 0
                ? $"https://steamcommunity.com/sharedfiles/filedetails/?id={publishedFileId!.Value}"
                : "",
            finalPlan.RunDirectory,
            finalPlan.VdfPath,
            finalPlan.ContentFolder,
            Truncate(steamResult.Stdout, 12000),
            Truncate(steamResult.Stderr, 12000),
            logTails,
            tagUpdate,
            finalPlan);
    }

    private async Task BuildReleaseAsync(ModInspection mod, string stagingRoot)
    {
        if (string.IsNullOrWhiteSpace(mod.ProjectPath))
            throw new InvalidOperationException("This mod path does not include a build project. Use the existing folder as Workshop content instead of building it.");

        var buildResult = await processRunner.RunAsync(
            "dotnet",
            ["build", mod.ProjectPath, "-c", "Release", $"-p:RIMWORLD_MOD_DIR={stagingRoot}"],
            workingDirectory: mod.RepoPath,
            timeout: TimeSpan.FromMinutes(5));

        if (buildResult.ExitCode != 0)
            throw new InvalidOperationException($"Release build failed with exit code {buildResult.ExitCode}.\nSTDOUT:\n{buildResult.Stdout}\nSTDERR:\n{buildResult.Stderr}");
    }

    private static ulong? TryReadPublishedFileId(string vdfPath)
    {
        if (!File.Exists(vdfPath))
            return null;

        var raw = VdfWriter.ReadWorkshopItemField(File.ReadAllText(vdfPath), "publishedfileid");
        return ulong.TryParse(raw, out var parsed) && parsed != 0 ? parsed : null;
    }

    private static string GetPublishedFileIdPath(ModInspection mod)
    {
        return mod.PublishedFileIdPath ?? Path.Combine(mod.RepoPath, "About", "PublishedFileId.txt");
    }

    private static string TailFile(string path, int maxLines)
    {
        var lines = File.ReadLines(path).TakeLast(maxLines);
        return $"==> {path}\n" + string.Join('\n', lines);
    }

    private static string Truncate(string value, int maxChars)
    {
        return value.Length <= maxChars
            ? value
            : value[^maxChars..];
    }
}
