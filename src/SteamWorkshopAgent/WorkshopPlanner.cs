namespace SteamWorkshopAgent;

public sealed class WorkshopPlanner(ModInspector modInspector, GitHubReleaseReader releaseReader)
{
    public async Task<WorkshopReleasePlan> CreateReleasePlanAsync(
        string repoPath,
        string tag,
        bool updateDescription = false,
        string? runDirectory = null,
        string? contentFolderOverride = null)
    {
        var mod = await modInspector.InspectAsync(repoPath);
        var release = await releaseReader.ReadAsync(mod.RepoPath, tag);
        var runDir = runDirectory ?? AgentPaths.NewRunDirectory(mod.ModFileName, tag);
        var stagingRoot = Path.Combine(runDir, "mods");
        var contentFolder = contentFolderOverride ?? Path.Combine(stagingRoot, mod.ModFileName);
        var previewFile = Path.Combine(contentFolder, "About", "Preview.png");
        var vdfPath = Path.Combine(runDir, "workshop.vdf");

        var fields = CreateVdfFields(mod, release.ChangeNote, contentFolder, previewFile, updateDescription);
        var vdfContent = VdfWriter.WriteWorkshopItem(fields);

        var validationPreviewPath = File.Exists(previewFile) ? previewFile : mod.PreviewImagePath ?? previewFile;
        var issues = Validation.ValidateForWorkshop(mod, validationPreviewPath, contentFolder)
            .Concat(new[]
            {
                new ValidationIssue(
                    "steamcmd_tags_preserved",
                    "SteamCMD workshop_build_item does not reliably document RimWorld-style tag updates; V1 preserves existing Workshop tags instead of attempting to rewrite them.",
                    "info")
            })
            .ToList();

        var intendedTags = new List<string> { "Mod" };
        intendedTags.AddRange(mod.SupportedVersions);

        return new WorkshopReleasePlan(
            mod,
            release,
            runDir,
            stagingRoot,
            contentFolder,
            previewFile,
            vdfPath,
            vdfContent,
            updateDescription,
            TagsPreserved: true,
            intendedTags,
            issues);
    }

    public static IReadOnlyDictionary<string, string> CreateVdfFields(
        ModInspection mod,
        string changeNote,
        string contentFolder,
        string previewFile,
        bool updateDescription)
    {
        if (mod.PublishedFileId is null)
            throw new InvalidOperationException("Cannot create a Workshop update VDF without PublishedFileId.");

        var fields = new Dictionary<string, string>
        {
            ["appid"] = AgentPaths.RimWorldAppId.ToString(),
            ["publishedfileid"] = mod.PublishedFileId.Value.ToString(),
            ["contentfolder"] = contentFolder,
            ["previewfile"] = previewFile,
            ["title"] = mod.ModName,
            ["changenote"] = changeNote
        };

        if (updateDescription)
            fields["description"] = mod.Description ?? "";

        return fields;
    }
}
