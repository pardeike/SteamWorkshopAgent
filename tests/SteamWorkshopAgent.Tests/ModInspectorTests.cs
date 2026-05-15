using System.Xml.Linq;
using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class ModInspectorTests
{
    [Fact]
    public async Task InspectAsync_Reads_RimWorld_Mod_Metadata()
    {
        using var fixture = TestModRepo.Create();
        var inspector = new ModInspector(new ProcessRunner());

        var result = await inspector.InspectAsync(fixture.Root);

        Assert.Equal("TestMod", result.ModFileName);
        Assert.Equal("Test Mod", result.ModName);
        Assert.Equal("brrainz.testmod", result.PackageId);
        Assert.Equal("1.2.3.4", result.ModVersion);
        Assert.Equal((ulong)123456789, result.PublishedFileId);
        Assert.Equal((uint)294100, result.RimWorldAppId);
        Assert.Contains("1.6", result.SupportedVersions);
        Assert.EndsWith("TestMod.csproj", result.ProjectPath);
        Assert.Equal("https://steamcommunity.com/sharedfiles/filedetails/?id=123456789", result.WorkshopUrl);
    }

    [Fact]
    public async Task InspectAsync_Does_Not_Read_Dependency_PackageId_As_Mod_PackageId()
    {
        using var fixture = TestModRepo.Create();
        var about = XDocument.Load(Path.Combine(fixture.Root, "About", "About.xml"));
        about.Root!.Element("modDependencies")!.AddFirst(new XElement("li", new XElement("packageId", "wrong.dependency")));
        about.Save(Path.Combine(fixture.Root, "About", "About.xml"));

        var inspector = new ModInspector(new ProcessRunner());
        var result = await inspector.InspectAsync(fixture.Root);

        Assert.Equal("brrainz.testmod", result.PackageId);
    }
}
