namespace SteamWorkshopAgent;

public sealed class WorkshopPublishVerifier(
    WorkshopPublishRequestStore requestStore,
    WorkshopDescriptionReader descriptionReader)
{
    public async Task<WorkshopPublishVerificationResult> VerifyAsync(
        string requestPath,
        int waitSeconds = 0)
    {
        if (waitSeconds is < 0 or > 180)
            throw new ArgumentOutOfRangeException(nameof(waitSeconds), "Wait time must be between 0 and 180 seconds.");

        var request = await requestStore.ReadForVerificationAsync(requestPath);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(waitSeconds);
        WorkshopDescriptionSnapshot snapshot;
        bool titleMatches;
        bool timestampMatches;
        do
        {
            snapshot = await descriptionReader.GetDescriptionAsync(request.PublishedFileId.ToString());
            titleMatches = string.Equals(snapshot.Title, request.Title, StringComparison.Ordinal);
            timestampMatches = snapshot.TimeUpdated is { } timestamp
                && DateTimeOffset.FromUnixTimeSeconds(timestamp) >= request.CreatedAtUtc.AddSeconds(-5);
            if (titleMatches && timestampMatches)
                break;
            if (DateTimeOffset.UtcNow >= deadline)
                break;
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        while (true);

        var success = snapshot.Result == 1 && titleMatches && timestampMatches;
        return new WorkshopPublishVerificationResult(
            success,
            request.PublishedFileId,
            snapshot.WorkshopUrl,
            request.Title,
            snapshot.Title,
            snapshot.TimeUpdated,
            request.CreatedAtUtc,
            titleMatches,
            timestampMatches,
            requestPath,
            success
                ? "Steam's public item details reflect the prepared Workshop update."
                : "Steam's public item details do not yet prove the prepared update. Do not resubmit solely from this result if submission already started.");
    }
}
