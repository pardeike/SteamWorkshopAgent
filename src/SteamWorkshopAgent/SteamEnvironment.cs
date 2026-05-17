namespace SteamWorkshopAgent;

public sealed class SteamEnvironment(ProcessRunner processRunner)
{
    public async Task<SteamStatusResult> GetStatusAsync(bool runSteamCmdQuit = false)
    {
        var steamCmdPath = processRunner.FindOnPath("steamcmd");
        var manifestPath = FindRimWorldManifest();
        var nativeLibraryPath = FindSteamworksNativeLibrary();
        var logPaths = GetWorkshopLogPaths().ToList();
        ProcessResult? quitResult = null;

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
            Environment.GetEnvironmentVariable("STEAMCMD_USER"),
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

    public string RequireSteamUser(string? steamUser)
    {
        var resolved = string.IsNullOrWhiteSpace(steamUser)
            ? Environment.GetEnvironmentVariable("STEAMCMD_USER")
            : steamUser;

        if (string.IsNullOrWhiteSpace(resolved))
            throw new InvalidOperationException("No Steam username was provided. Pass steamUser to workshop.publish_release or set STEAMCMD_USER. Passwords and Steam Guard codes are never stored by this agent.");

        return resolved.Trim();
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
}
