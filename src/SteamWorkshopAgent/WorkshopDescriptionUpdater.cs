using System.Text.Json;

namespace SteamWorkshopAgent;

public sealed class WorkshopDescriptionUpdater(
    ProcessRunner processRunner,
    SteamEnvironment steamEnvironment,
    WorkshopTargetResolver targetResolver)
{
    public async Task<object> UpdateDescriptionAsync(
        string modPathOrPublishedFileId,
        string description,
        bool confirm,
        string? steamUser = null,
        string? title = null,
        string? changeNote = null)
    {
        var plan = await CreateDescriptionPlanAsync(modPathOrPublishedFileId, description, title, changeNote);
        if (!confirm)
            return plan;

        Validation.ThrowIfErrors(plan.ValidationIssues);
        Directory.CreateDirectory(plan.RunDirectory);
        await File.WriteAllTextAsync(plan.VdfPath, plan.VdfContent);
        await File.WriteAllTextAsync(
            Path.Combine(plan.RunDirectory, "plan.json"),
            JsonSerializer.Serialize(plan, ToolJson.Options));

        var steamCmdPath = steamEnvironment.RequireSteamCmd();
        var resolvedSteamUser = await steamEnvironment.RequireSteamUserAsync(steamUser);
        var steamResult = await processRunner.RunAsync(
            steamCmdPath,
            ["+login", resolvedSteamUser, "+workshop_build_item", plan.VdfPath, "+quit"],
            workingDirectory: plan.RunDirectory,
            timeout: TimeSpan.FromMinutes(5));

        var logTails = steamEnvironment.GetWorkshopLogPaths()
            .Where(File.Exists)
            .Select(path => TailFile(path, 80))
            .ToList();

        return new WorkshopDescriptionUpdateResult(
            steamResult.ExitCode == 0,
            steamResult.ExitCode,
            plan.WorkshopUrl,
            plan.RunDirectory,
            plan.VdfPath,
            Truncate(steamResult.Stdout, 12000),
            Truncate(steamResult.Stderr, 12000),
            logTails,
            plan);
    }

    public async Task<WorkshopDescriptionPlan> CreateDescriptionPlanAsync(
        string modPathOrPublishedFileId,
        string description,
        string? title = null,
        string? changeNote = null)
    {
        var target = await targetResolver.ResolveDescriptionTargetAsync(modPathOrPublishedFileId);
        var runDirectory = AgentPaths.NewRunDirectory(
            target.ModName ?? target.PublishedFileId.ToString(),
            "description");
        var vdfPath = Path.Combine(runDirectory, "workshop.vdf");
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(description))
            issues.Add(new ValidationIssue(
                "description_empty",
                "Workshop description is empty.",
                "error"));

        if (description.Length > 8000)
            issues.Add(new ValidationIssue(
                "description_long",
                "Workshop description is longer than 8000 characters; Steam may reject or truncate very long descriptions.",
                "warning"));

        var fields = new Dictionary<string, string>
        {
            ["appid"] = AgentPaths.RimWorldAppId.ToString(),
            ["publishedfileid"] = target.PublishedFileId.ToString(),
            ["description"] = description
        };

        if (!string.IsNullOrWhiteSpace(title))
            fields["title"] = title.Trim();

        if (!string.IsNullOrWhiteSpace(changeNote))
            fields["changenote"] = changeNote.Trim();

        return new WorkshopDescriptionPlan(
            target.PublishedFileId,
            target.ModPath,
            target.ModName,
            $"https://steamcommunity.com/sharedfiles/filedetails/?id={target.PublishedFileId}",
            AgentPaths.RimWorldAppId,
            runDirectory,
            vdfPath,
            description,
            description.Length,
            string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            string.IsNullOrWhiteSpace(changeNote) ? null : changeNote.Trim(),
            VdfWriter.WriteWorkshopItem(fields),
            issues);
    }

    private static string TailFile(string path, int lines)
    {
        var allLines = File.ReadLines(path).TakeLast(lines);
        return $"==> {path} <==\n{string.Join('\n', allLines)}";
    }

    private static string Truncate(string value, int maxChars)
    {
        return value.Length <= maxChars ? value : string.Concat(value.AsSpan(0, maxChars), "\n...[truncated]");
    }
}
