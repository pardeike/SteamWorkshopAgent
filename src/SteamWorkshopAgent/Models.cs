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
    string ProjectPath,
    string AboutXmlPath,
    string? LoadFoldersPath,
    string? PublishedFileIdPath,
    ulong? PublishedFileId,
    string? PreviewImagePath,
    long? PreviewImageBytes,
    IReadOnlyList<string> SupportedVersions,
    string? Description,
    uint RimWorldAppId,
    string WorkshopUrl);

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

public sealed record SteamStatusResult(
    string? SteamCmdPath,
    bool SteamCmdFound,
    string? SteamCmdUser,
    bool SteamAppManifestFound,
    string? SteamAppManifestPath,
    uint RimWorldAppId,
    IReadOnlyList<string> WorkshopLogPaths,
    ProcessResult? SteamCmdQuitResult,
    string SetupHint);

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
    WorkshopReleasePlan Plan);

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
