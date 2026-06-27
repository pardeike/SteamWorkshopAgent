namespace SteamWorkshopAgent;

public sealed class SteamEnvironment(ProcessRunner processRunner)
{
    internal const string SteamUserProbePrefix = "__STEAM_WORKSHOP_AGENT_STEAMCMD_USER__";
    internal const string SteamUserProbeSuffix = "__END__";

    public async Task<SteamStatusResult> GetStatusAsync(bool runSteamCmdQuit = false)
    {
        var steamCmdPath = processRunner.FindOnPath("steamcmd");
        var manifestPath = FindRimWorldManifest();
        var nativeLibraryPath = FindSteamworksNativeLibrary();
        var logPaths = GetWorkshopLogPaths().ToList();
        ProcessResult? quitResult = null;
        var steamCmdUser = await ResolveSteamUserAsync(null);

        if (runSteamCmdQuit && steamCmdPath != null)
        {
            quitResult = await processRunner.RunAsync(
                steamCmdPath,
                ["+quit"],
                timeout: TimeSpan.FromSeconds(30));
        }

        var setupHint = steamCmdPath == null
            ? "Install SteamCMD first, for example: brew install steamcmd"
            : "Run steamcmd +login <steam_user> +quit once interactively to establish Steam Guard/session state. Future runs should pass only the username; SteamCMD reuses the login token from Steam/config/config.vdf.";
        var tagUpdateHint = nativeLibraryPath == null
            ? "RimWorld's native Steam API library was not found. Install RimWorld through Steam or set STEAMWORKS_NATIVE_LIB."
            : "Tag updates use the desktop Steamworks session, not the SteamCMD login token. The desktop Steam client must be online and logged on; if Steam shows NO CONNECTION, tag updates return NotLoggedOn.";

        return new SteamStatusResult(
            steamCmdPath,
            steamCmdPath != null,
            steamCmdUser,
            manifestPath != null,
            manifestPath,
            nativeLibraryPath != null,
            nativeLibraryPath,
            AgentPaths.RimWorldAppId,
            logPaths,
            quitResult,
            setupHint,
            tagUpdateHint);
    }

    public string RequireSteamCmd()
    {
        return processRunner.FindOnPath("steamcmd")
            ?? throw new InvalidOperationException("steamcmd was not found on PATH. Install it with `brew install --cask steamcmd`, then run `steamcmd +login <steam_user> +quit` once interactively.");
    }

    public async Task<string> RequireSteamUserAsync(string? steamUser)
    {
        return await ResolveSteamUserAsync(steamUser)
            ?? throw new InvalidOperationException("No Steam username was provided. Pass steamUser to WorkshopPublishRelease or WorkshopUpdateDescription, set STEAMCMD_USER in the MCP environment, or export it from your shell startup files. Passwords and Steam Guard codes are never stored by this agent.");
    }

    private async Task<string?> ResolveSteamUserAsync(string? steamUser)
    {
        if (!string.IsNullOrWhiteSpace(steamUser))
            return steamUser.Trim();

        var inherited = Environment.GetEnvironmentVariable("STEAMCMD_USER");
        if (!string.IsNullOrWhiteSpace(inherited))
            return inherited.Trim();

        var fromShell = await TryReadSteamUserFromShellAsync();
        if (!string.IsNullOrWhiteSpace(fromShell))
            return fromShell.Trim();

        return null;
    }

    public IEnumerable<string> GetWorkshopLogPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var steamRoot = Path.Combine(home, "Library", "Application Support", "Steam");

        yield return Path.Combine(steamRoot, "logs", "workshop_log.txt");
        yield return Path.Combine(steamRoot, "workshopbuilds", $"depot_build_{AgentPaths.RimWorldAppId}.log");
    }

    public string? FindSteamworksNativeLibrary()
    {
        var configured = Environment.GetEnvironmentVariable("STEAMWORKS_NATIVE_LIB");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Path.GetFullPath(ExpandHome(configured));
            if (File.Exists(path))
                return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "RimWorld", "RimWorldMac.app", "Contents", "PlugIns", "steam_api.bundle", "Contents", "MacOS", "libsteam_api.dylib")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindRimWorldManifest()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", $"appmanifest_{AgentPaths.RimWorldAppId}.acf"),
            Path.Combine("/Applications", "Steam.app", "Contents", "MacOS", "steamapps", $"appmanifest_{AgentPaths.RimWorldAppId}.acf")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith("~/", StringComparison.Ordinal))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return path;
    }

    private async Task<string?> TryReadSteamUserFromShellAsync()
    {
        var shellPath = ResolveShellPath();
        if (shellPath == null)
            return null;

        try
        {
            var result = await processRunner.RunAsync(
                shellPath,
                CreateShellArguments(shellPath),
                timeout: TimeSpan.FromSeconds(5),
                maxOutputChars: 4096);

            return result.ExitCode == 0
                ? ExtractSteamUserFromShellOutput(result.Stdout)
                : null;
        }
        catch
        {
            return null;
        }
    }

    internal static string? ExtractSteamUserFromShellOutput(string stdout)
    {
        var start = stdout.LastIndexOf(SteamUserProbePrefix, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += SteamUserProbePrefix.Length;
        var end = stdout.IndexOf(SteamUserProbeSuffix, start, StringComparison.Ordinal);
        if (end < 0)
            return null;

        var value = stdout[start..end].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ResolveShellPath()
    {
        var configured = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        return File.Exists("/bin/zsh")
            ? "/bin/zsh"
            : File.Exists("/bin/sh")
                ? "/bin/sh"
                : null;
    }

    private static string[] CreateShellArguments(string shellPath)
    {
        var command = $"printf '\\n{SteamUserProbePrefix}%s{SteamUserProbeSuffix}\\n' \"${{STEAMCMD_USER:-}}\"";
        var shellName = Path.GetFileName(shellPath);

        return shellName is "zsh" or "bash"
            ? ["-lic", command]
            : shellName is "sh" or "ksh"
                ? ["-lc", command]
                : ["-c", command];
    }
}
