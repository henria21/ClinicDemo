using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Services;
using ClinicDemo.Core.Session;

namespace ClinicDemo.Web;

/// <summary>
/// DI composition for the demo. Blazor WebAssembly runs one app per browser tab, so singletons are per tab:
/// a refresh starts from an empty store (FR-18). No HttpClient, no external services (NFR-3).
/// </summary>
public static class ClinicDemoServices
{
    public static IServiceCollection AddClinicDemo(this IServiceCollection services)
    {
        services.AddSingleton(DemoOptions.Default);
        services.AddSingleton<HebrewTextNormalizer>();
        services.AddSingleton<HebrewPrefixMatcher>();
        services.AddSingleton<HebrewPreferenceExtractor>();
        services.AddSingleton<VisitorIntentDetector>();
        services.AddSingleton<HebrewDateResolver>();
        services.AddSingleton<RequestValidator>();
        services.AddSingleton<HebrewFormatter>();
        services.AddSingleton<MessageCatalog>();
        services.AddSingleton<RequestStore>();
        services.AddSingleton<ConversationEngine>();
        services.AddSingleton<DemoAppState>();
        return services;
    }
}
