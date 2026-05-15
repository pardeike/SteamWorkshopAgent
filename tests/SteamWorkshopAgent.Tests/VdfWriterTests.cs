using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class VdfWriterTests
{
    [Fact]
    public void Escape_Handles_Quotes_Backslashes_And_Preserves_Newlines()
    {
        var escaped = VdfWriter.Escape("C:\\Mods\\A \"quoted\"\nline");

        Assert.Equal("C:\\\\Mods\\\\A \\\"quoted\\\"\nline", escaped);
    }

    [Fact]
    public void WriteWorkshopItem_Writes_Real_Newlines_In_Changenote()
    {
        var vdf = VdfWriter.WriteWorkshopItem(new Dictionary<string, string>
        {
            ["changenote"] = "Line one\nLine two"
        });

        Assert.Contains("\"changenote\" \"Line one\nLine two\"", vdf);
        Assert.DoesNotContain("\\nLine two", vdf);
    }

    [Fact]
    public void CreateVdfFields_Omits_Description_By_Default()
    {
        var mod = TestModRepo.SampleInspection(description: "Long description");
        var fields = WorkshopPlanner.CreateVdfFields(mod, "Release notes", "/tmp/content", "/tmp/preview.png", updateDescription: false);

        Assert.False(fields.ContainsKey("description"));
        Assert.Equal("294100", fields["appid"]);
        Assert.Equal("123456789", fields["publishedfileid"]);
        Assert.Equal("Release notes", fields["changenote"]);
    }

    [Fact]
    public void CreateVdfFields_Includes_Description_When_Requested()
    {
        var mod = TestModRepo.SampleInspection(description: "Long description");
        var fields = WorkshopPlanner.CreateVdfFields(mod, "Release notes", "/tmp/content", "/tmp/preview.png", updateDescription: true);

        Assert.Equal("Long description", fields["description"]);
    }
}
