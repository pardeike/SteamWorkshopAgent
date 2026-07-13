using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class WorkshopPublishRequestStoreTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "steam-workshop-agent-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ContentDigest_Is_Deterministic_And_Path_Sensitive()
    {
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "two");
        await File.WriteAllTextAsync(Path.Combine(root, "nested", "a.txt"), "one");

        var first = await WorkshopPublishRequestStore.ComputeContentDigestAsync(root);
        var second = await WorkshopPublishRequestStore.ComputeContentDigestAsync(root);

        Assert.Equal(first, second);

        File.Move(Path.Combine(root, "b.txt"), Path.Combine(root, "renamed.txt"));
        var renamed = await WorkshopPublishRequestStore.ComputeContentDigestAsync(root);
        Assert.NotEqual(first, renamed);
    }

    [Fact]
    public async Task ContentDigest_Changes_When_File_Content_Changes()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "content.bin");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        var before = await WorkshopPublishRequestStore.ComputeContentDigestAsync(root);

        await File.WriteAllBytesAsync(path, [1, 2, 4]);
        var after = await WorkshopPublishRequestStore.ComputeContentDigestAsync(root);

        Assert.NotEqual(before, after);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
