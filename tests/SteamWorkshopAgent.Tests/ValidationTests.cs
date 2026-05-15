using SteamWorkshopAgent;

namespace SteamWorkshopAgent.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void ValidateForWorkshop_Rejects_Missing_Published_File_Id()
    {
        using var fixture = TestModRepo.Create();
        var mod = TestModRepo.SampleInspection(publishedFileId: null, previewImagePath: fixture.PreviewPath);

        var issues = Validation.ValidateForWorkshop(mod, fixture.PreviewPath, fixture.Root);

        Assert.Contains(issues, issue => issue.Code == "missing_published_file_id" && issue.Severity == "error");
    }

    [Fact]
    public void ValidateForWorkshop_Rejects_Oversized_Preview()
    {
        using var fixture = TestModRepo.Create(previewBytes: (1024 * 1024) + 1);
        var mod = TestModRepo.SampleInspection(previewImagePath: fixture.PreviewPath);

        var issues = Validation.ValidateForWorkshop(mod, fixture.PreviewPath, fixture.Root);

        Assert.Contains(issues, issue => issue.Code == "preview_too_large" && issue.Severity == "error");
    }
}
