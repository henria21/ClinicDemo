using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Session;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicDemo.Web.Tests;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection().AddClinicDemo().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

    [Fact]
    public void DemoAppState_ResolvesFromTheWebServiceCollection()
    {
        using var provider = BuildProvider();

        var state = provider.GetRequiredService<DemoAppState>();

        Assert.Equal(DemoOptions.Default, state.Options);
        Assert.Null(state.Session);
    }

    [Fact]
    public void TheStoreAndAppState_AreSingletons()
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<DemoAppState>(), provider.GetRequiredService<DemoAppState>());
        Assert.Same(provider.GetRequiredService<RequestStore>(), provider.GetRequiredService<RequestStore>());
    }

    [Fact]
    public void TheResolvedAppState_RunsAppendixA()
    {
        using var provider = BuildProvider();
        var state = provider.GetRequiredService<DemoAppState>();

        state.SelectContact("DEMO-A");
        state.Send("שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?");
        state.Send("יום שלישי אחרי 16:00.");
        var result = state.Submit(state.Session!.ActiveReview!.ReviewId);

        Assert.Equal(SubmitOutcome.Created, result.Outcome);
        Assert.Equal("REQ-0001", result.Request!.Id);
        Assert.Equal(result.Request, provider.GetRequiredService<RequestStore>().GetAll().Single());
    }

    [Fact]
    public void NoHttpClient_IsRegistered()
    {
        var services = new ServiceCollection().AddClinicDemo();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(HttpClient));
    }
}
