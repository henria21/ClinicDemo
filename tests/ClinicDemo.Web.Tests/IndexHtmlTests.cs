namespace ClinicDemo.Web.Tests;

public class IndexHtmlTests
{
    private static string ReadIndexHtml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ClinicDemo.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "ClinicDemo.Web", "wwwroot", "index.html"));
    }

    [Fact]
    public void IndexHtml_HasNoExternalReferences()
    {
        Assert.DoesNotMatch("https?://", ReadIndexHtml());
    }

    [Fact]
    public void IndexHtml_IsHebrewRtl()
    {
        var html = ReadIndexHtml();
        Assert.Contains("lang=\"he\"", html);
        Assert.Contains("dir=\"rtl\"", html);
    }
}
