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
    public void WriteWorkshopItem_Writes_Tag_Block()
    {
        var vdf = VdfWriter.WriteWorkshopItem(
            new Dictionary<string, string>
            {
                ["appid"] = "294100"
            },
            ["Mod", "1.6"]);

        Assert.Contains("\"tags\"", vdf);
        Assert.Contains("\"0\" \"Mod\"", vdf);
        Assert.Contains("\"1\" \"1.6\"", vdf);
    }

    [Fact]
    public void ReadWorkshopItemField_Reads_Steamcmd_Updated_Id()
    {
        var vdf = """
            "workshopitem"
            {
              "appid" "294100"
              "publishedfileid" "987654321"
            }
            """;

        var value = VdfWriter.ReadWorkshopItemField(vdf, "publishedfileid");

        Assert.Equal("987654321", value);
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

    [Fact]
    public void CreateNewModVdfFields_Uses_Create_Id_And_Private_Visibility()
    {
        var mod = TestModRepo.SampleInspection(publishedFileId: null, description: "Initial description");

        var fields = WorkshopPlanner.CreateNewModVdfFields(
            mod,
            "/tmp/content",
            "/tmp/preview.png",
            "private",
            "Initial upload");

        Assert.Equal("294100", fields["appid"]);
        Assert.Equal("0", fields["publishedfileid"]);
        Assert.Equal("2", fields["visibility"]);
        Assert.Equal("Initial description", fields["description"]);
        Assert.Equal("Initial upload", fields["changenote"]);
    }

    [Fact]
    public void CreateDefaultTags_Includes_Mod_And_Supported_Versions()
    {
        var mod = TestModRepo.SampleInspection();

        var tags = WorkshopPlanner.CreateDefaultTags(mod);

        Assert.Equal(["Mod", "1.6"], tags);
    }

    [Theory]
    [InlineData("public", "0")]
    [InlineData("friends", "1")]
    [InlineData("private", "2")]
    [InlineData("unlisted", "3")]
    [InlineData("0", "0")]
    public void NormalizeVisibility_Accepts_Names_And_Values(string input, string expected)
    {
        Assert.Equal(expected, WorkshopPlanner.NormalizeVisibility(input));
    }
}
