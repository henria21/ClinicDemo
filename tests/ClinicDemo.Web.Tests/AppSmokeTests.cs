using Bunit;

namespace ClinicDemo.Web.Tests;

public class AppSmokeTests : TestContext
{
    [Fact]
    public void App_Renders_WithoutError()
    {
        Services.AddClinicDemo();

        var cut = RenderComponent<App>();

        Assert.NotNull(cut.Markup);
    }
}
