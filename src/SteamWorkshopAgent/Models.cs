namespace SteamWorkshopAgent;

public sealed record ModInspection(
    string RepoPath,
    string ModFileName,
    string ModName,
    string PackageId,
    string ModVersion,
    string? RepositoryUrl,
    string? GitRemoteUrl,
    string? GitBranch,
    string? ProjectPath,
    string AboutXmlPath,
    string? LoadFoldersPath,
    string? PublishedFileIdPath,
    ulong? PublishedFileId,
    string? PreviewImagePath,
    long? PreviewImageBytes,
    IReadOnlyList<string> SupportedVersions,
    string? Description,
    uint RimWorldAppId,
    string WorkshopUrl)
{
    public bool HasBuildProject => !string.IsNullOrWhiteSpace(ProjectPath);
}

public sealed record GitHubReleaseInfo(
    string TagName,
    string Name,
    string Body,
    string Url,
    string ChangeNote);

public sealed record ValidationIssue(string Code, string Message, string Severity);

public sealed record WorkshopReleasePlan(
    ModInspection Mod,
    GitHubReleaseInfo Release,
    string RunDirectory,
    string StagingRoot,
    string ContentFolder,
    string PreviewFile,
    string VdfPath,
    string VdfContent,
    bool UpdateDescription,
    bool TagsPreserved,
    IReadOnlyList<string> IntendedTags,
    IReadOnlyList<ValidationIssue> ValidationIssues);

public sealed record WorkshopNewModPlan(
    ModInspection Mod,
    string RunDirectory,
    string StagingRoot,
    string ContentFolder,
    string PreviewFile,
    string VdfPath,
    string VdfContent,
    string Visibility,
    string ChangeNote,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ValidationIssue> ValidationIssues);

public sealed record SteamStatusResult(
    string? SteamCmdPath,
    bool SteamCmdFound,
    string? SteamCmdUser,
    bool SteamAppManifestFound,
    string? SteamAppManifestPath,
    bool SteamworksNativeLibraryFound,
    string? SteamworksNativeLibraryPath,
    uint RimWorldAppId,
    IReadOnlyList<string> WorkshopLogPaths,
    ProcessResult? SteamCmdQuitResult,
    string SetupHint,
    string TagUpdateHint);

public sealed record PublishResult(
    bool Success,
    int SteamCmdExitCode,
    string WorkshopUrl,
    string RunDirectory,
    string VdfPath,
    string ContentFolder,
    string SteamCmdStdout,
    string SteamCmdStderr,
    IReadOnlyList<string> LogTails,
    WorkshopTagUpdateResult? TagUpdate,
    WorkshopReleasePlan Plan);

public sealed record CreateNewModResult(
    bool Success,
    int SteamCmdExitCode,
    ulong? PublishedFileId,
    string PublishedFileIdPath,
    string WorkshopUrl,
    string RunDirectory,
    string VdfPath,
    string ContentFolder,
    string SteamCmdStdout,
    string SteamCmdStderr,
    IReadOnlyList<string> LogTails,
    WorkshopTagUpdateResult? TagUpdate,
    WorkshopNewModPlan Plan);

public sealed record WorkshopTagUpdatePlan(
    ulong PublishedFileId,
    IReadOnlyList<string> Tags,
    string ChangeNote,
    uint RimWorldAppId,
    string? NativeLibraryPath,
    IReadOnlyList<ValidationIssue> ValidationIssues);

public sealed record WorkshopTagUpdateResult(
    bool Success,
    ulong PublishedFileId,
    IReadOnlyList<string> Tags,
    string ChangeNote,
    string Backend,
    string? NativeLibraryPath,
    bool SteamInitialized,
    bool SteamUserLoggedOn,
    uint? SteamAppId,
    string? SubmitResult,
    bool UserNeedsToAcceptWorkshopLegalAgreement,
    bool TimedOut,
    string Message);

public sealed record ProcessResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    long DurationMs,
    bool TimedOut,
    bool StdoutTruncated,
    bool StderrTruncated,
    long StdoutChars,
    long StderrChars);
