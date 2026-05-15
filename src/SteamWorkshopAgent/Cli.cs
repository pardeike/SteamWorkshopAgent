namespace SteamWorkshopAgent;

internal static class Cli
{
    public static bool ShouldHandle(string[] args)
    {
        return args.Length > 0
            && !string.Equals(args[0], "server", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var command = args[0].ToLowerInvariant();
            var services = CreateServices();
            var data = command switch
            {
                "status" => await services.SteamEnvironment.GetStatusAsync(args.Contains("--run-steamcmd-quit")),
                "inspect" => await services.ModInspector.InspectAsync(RequireArg(args, 1, "repoPath")),
                "plan" => await services.WorkshopPlanner.CreateReleasePlanAsync(
                    RequireArg(args, 1, "repoPath"),
                    RequireArg(args, 2, "tag"),
                    HasFlag(args, "--update-description")),
                "publish" => await services.WorkshopPublisher.PublishReleaseAsync(
                    RequireArg(args, 1, "repoPath"),
                    RequireArg(args, 2, "tag"),
                    HasFlag(args, "--confirm"),
                    HasFlag(args, "--update-description"),
                    GetOption(args, "--steam-user")),
                "help" or "--help" or "-h" => Usage(),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.\n{Usage()}")
            };

            Console.WriteLine(ToolJson.Ok(data));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(ToolJson.Error(exception));
            return 1;
        }
    }

    private static Services CreateServices()
    {
        var processRunner = new ProcessRunner();
        var steamEnvironment = new SteamEnvironment(processRunner);
        var modInspector = new ModInspector(processRunner);
        var releaseReader = new GitHubReleaseReader(processRunner);
        var planner = new WorkshopPlanner(modInspector, releaseReader);
        var publisher = new WorkshopPublisher(processRunner, steamEnvironment, planner);

        return new Services(
            steamEnvironment,
            modInspector,
            planner,
            publisher);
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Contains(flag, StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static string RequireArg(string[] args, int index, string name)
    {
        if (args.Length <= index || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing required argument: {name}\n{Usage()}");

        return args[index];
    }

    private static string Usage()
    {
        return """
            Usage:
              SteamWorkshopAgent server
              SteamWorkshopAgent status [--run-steamcmd-quit]
              SteamWorkshopAgent inspect <repoPath>
              SteamWorkshopAgent plan <repoPath> <tag> [--update-description]
              SteamWorkshopAgent publish <repoPath> <tag> [--confirm] [--steam-user USER] [--update-description]
            """;
    }

    private sealed record Services(
        SteamEnvironment SteamEnvironment,
        ModInspector ModInspector,
        WorkshopPlanner WorkshopPlanner,
        WorkshopPublisher WorkshopPublisher);
}
