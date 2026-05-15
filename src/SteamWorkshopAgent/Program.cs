using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SteamWorkshopAgent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (Cli.ShouldHandle(args))
        {
            Environment.ExitCode = await Cli.RunAsync(args);
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new StderrLoggerProvider());
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddSingleton<ProcessRunner>();
        builder.Services.AddSingleton<SteamEnvironment>();
        builder.Services.AddSingleton<ModInspector>();
        builder.Services.AddSingleton<GitHubReleaseReader>();
        builder.Services.AddSingleton<WorkshopPlanner>();
        builder.Services.AddSingleton<WorkshopPublisher>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var app = builder.Build();
        ServiceLocator.SetProvider(app.Services);
        await app.RunAsync();
    }
}
