namespace SteamWorkshopAgent;

public sealed class WorkshopTargetResolver(ModInspector modInspector)
{
    public async Task<WorkshopDescriptionTarget> ResolveDescriptionTargetAsync(string modPathOrPublishedFileId)
    {
        if (ulong.TryParse(modPathOrPublishedFileId.Trim(), out var publishedFileId) && publishedFileId != 0)
            return new WorkshopDescriptionTarget(publishedFileId, null, null);

        var mod = await modInspector.InspectAsync(modPathOrPublishedFileId);
        if (mod.PublishedFileId is not { } id || id == 0)
            throw new InvalidOperationException("About/PublishedFileId.txt is required when resolving a Workshop item from a mod path.");

        return new WorkshopDescriptionTarget(id, mod.RepoPath, mod.ModName);
    }
}
