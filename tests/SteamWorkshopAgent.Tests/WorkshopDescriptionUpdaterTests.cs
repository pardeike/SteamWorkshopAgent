using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class WorkshopDescriptionUpdaterTests
{
    [Fact]
    public async Task CreateDescriptionPlan_FromModPath_WritesDescriptionOnlyVdf()
    {
        using var fixture = TestModRepo.Create();
        var updater = CreateUpdater();

        var plan = await updater.CreateDescriptionPlanAsync(fixture.Root, "New main description");

        var fields = VdfWriter.ReadWorkshopItemFields(plan.VdfContent);
        Assert.Equal("294100", fields["appid"]);
        Assert.Equal("123456789", fields["publishedfileid"]);
        Assert.Equal("New main description", fields["description"]);
        Assert.False(fields.ContainsKey("contentfolder"));
        Assert.False(fields.ContainsKey("previewfile"));
        Assert.False(fields.ContainsKey("title"));
        Assert.False(fields.ContainsKey("changenote"));
        Assert.DoesNotContain(plan.ValidationIssues, issue => issue.Severity == "error");
    }

    [Fact]
    public async Task CreateDescriptionPlan_FromPublishedFileId_DoesNotRequireLocalMod()
    {
        var updater = CreateUpdater();

        var plan = await updater.CreateDescriptionPlanAsync("928376710", "Updated text");

        Assert.Equal(928376710UL, plan.PublishedFileId);
        Assert.Null(plan.ModPath);
        Assert.Equal("https://steamcommunity.com/sharedfiles/filedetails/?id=928376710", plan.WorkshopUrl);
    }

    [Fact]
    public async Task CreateDescriptionPlan_IncludesOptionalTitleAndChangeNote()
    {
        var updater = CreateUpdater();

        var plan = await updater.CreateDescriptionPlanAsync(
            "928376710",
            "Updated text",
            title: "Better Title",
            changeNote: "Updated Workshop description.");

        var fields = VdfWriter.ReadWorkshopItemFields(plan.VdfContent);
        Assert.Equal("Better Title", fields["title"]);
        Assert.Equal("Updated Workshop description.", fields["changenote"]);
    }

    private static WorkshopDescriptionUpdater CreateUpdater()
    {
        var processRunner = new ProcessRunner();
        return new WorkshopDescriptionUpdater(
            processRunner,
            new SteamEnvironment(processRunner),
            new WorkshopTargetResolver(new ModInspector(processRunner)));
    }
}
