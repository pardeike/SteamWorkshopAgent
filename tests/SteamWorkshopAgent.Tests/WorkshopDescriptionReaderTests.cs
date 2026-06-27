using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class WorkshopDescriptionReaderTests
{
    [Fact]
    public void ParseSnapshot_ReturnsCurrentDescriptionFields()
    {
        var target = new WorkshopDescriptionTarget(928376710, "/tmp/Zombieland", "Zombieland");

        var snapshot = WorkshopDescriptionReader.ParseSnapshot(
            """
            {
              "response": {
                "publishedfiledetails": [
                  {
                    "result": 1,
                    "publishedfileid": "928376710",
                    "creator": "76561197961067423",
                    "consumer_app_id": 294100,
                    "title": "Zombieland",
                    "description": "Current main description",
                    "visibility": 0,
                    "time_created": 1494850000,
                    "time_updated": 1782323598,
                    "preview_url": "https://example.invalid/preview.png",
                    "tags": [
                      { "tag": "Mod", "display_name": "Mod" },
                      { "tag": "1.6", "display_name": "1.6" }
                    ]
                  }
                ]
              }
            }
            """,
            target);

        Assert.Equal(928376710UL, snapshot.PublishedFileId);
        Assert.Equal("/tmp/Zombieland", snapshot.ModPath);
        Assert.Equal("Zombieland", snapshot.ModName);
        Assert.Equal("Zombieland", snapshot.Title);
        Assert.Equal("Current main description", snapshot.Description);
        Assert.Equal(24, snapshot.DescriptionCharacters);
        Assert.Equal(0, snapshot.Visibility);
        Assert.Equal(1782323598, snapshot.TimeUpdated);
        Assert.Equal(294100U, snapshot.ConsumerAppId);
        Assert.Equal(["Mod", "1.6"], snapshot.Tags);
        Assert.DoesNotContain(snapshot.ValidationIssues, issue => issue.Severity == "warning");
    }
}
