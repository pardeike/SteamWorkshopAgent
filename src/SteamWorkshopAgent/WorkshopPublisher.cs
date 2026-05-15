using System.Text.Json;

namespace SteamWorkshopAgent;

public sealed class WorkshopPublisher(
    ProcessRunner processRunner,
    SteamEnvironment steamEnvironment,
    WorkshopPlanner workshopPlanner)
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

        var buildResult = await processRunner.RunAsync(
            "dotnet",
            ["build", initialPlan.Mod.ProjectPath, "-c", "Release", $"-p:RIMWORLD_MOD_DIR={initialPlan.StagingRoot}"],
            workingDirectory: initialPlan.Mod.RepoPath,
            timeout: TimeSpan.FromMinutes(5));

        if (buildResult.ExitCode != 0)
            throw new InvalidOperationException($"Release build failed with exit code {buildResult.ExitCode}.\nSTDOUT:\n{buildResult.Stdout}\nSTDERR:\n{buildResult.Stderr}");

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
            finalPlan);
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
