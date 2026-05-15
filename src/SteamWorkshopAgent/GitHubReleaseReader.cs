using System.Text.Json;

namespace SteamWorkshopAgent;

public sealed class GitHubReleaseReader(ProcessRunner processRunner)
{
    public async Task<GitHubReleaseInfo> ReadAsync(string repoPath, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Release tag is required.", nameof(tag));

        var result = await processRunner.RunAsync(
            "gh",
            ["release", "view", tag, "--json", "body,name,tagName,url"],
            workingDirectory: repoPath,
            timeout: TimeSpan.FromSeconds(30));

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to read GitHub release {tag}: {result.Stderr.Trim()}");

        using var document = JsonDocument.Parse(result.Stdout);
        var root = document.RootElement;
        var tagName = GetString(root, "tagName") ?? tag;
        var name = GetString(root, "name") ?? tagName;
        var body = GetString(root, "body") ?? "";
        var url = GetString(root, "url") ?? "";

        var changeNote = NormalizeChangeNote(name, body, url);
        return new GitHubReleaseInfo(tagName, name, body, url, changeNote);
    }

    private static string NormalizeChangeNote(string name, string body, string url)
    {
        var text = string.IsNullOrWhiteSpace(body) ? name : body.Trim();
        if (!string.IsNullOrWhiteSpace(url) && !text.Contains(url, StringComparison.OrdinalIgnoreCase))
            text = $"{text.TrimEnd()}\n\nGitHub release: {url}";
        return text;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
