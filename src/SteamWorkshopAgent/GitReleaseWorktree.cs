namespace SteamWorkshopAgent;

public sealed class GitReleaseWorktree(ProcessRunner processRunner)
{
    public async Task<GitReleaseWorktreeHandle> CreateAsync(
        string repoPath,
        string tag,
        string runDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Release tag is required.", nameof(tag));

        var sourcePath = Path.Combine(runDirectory, "source");
        Directory.CreateDirectory(runDirectory);

        var result = await processRunner.RunAsync(
            "git",
            ["-C", repoPath, "worktree", "add", "--detach", sourcePath, tag],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Failed to create a detached release worktree for {tag}. Make sure the tag exists locally, or run `git fetch --tags` first.\nSTDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}");

        return new GitReleaseWorktreeHandle(processRunner, repoPath, sourcePath);
    }
}

public sealed class GitReleaseWorktreeHandle(
    ProcessRunner processRunner,
    string ownerRepoPath,
    string repoPath) : IAsyncDisposable
{
    public string RepoPath { get; } = repoPath;

    public async ValueTask DisposeAsync()
    {
        try
        {
            var result = await processRunner.RunAsync(
                "git",
                ["-C", ownerRepoPath, "worktree", "remove", "--force", RepoPath],
                timeout: TimeSpan.FromMinutes(1));

            if (result.ExitCode == 0)
                return;
        }
        catch
        {
            // Best effort cleanup; do not mask a successful publish with a cleanup issue.
        }

        TryDeleteDirectory(RepoPath);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The run directory remains available for manual cleanup if git cannot remove it.
        }
    }
}
