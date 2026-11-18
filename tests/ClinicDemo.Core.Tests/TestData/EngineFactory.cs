using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;
using ClinicDemo.Core.Session;

namespace ClinicDemo.Core.Tests.TestData;

/// <summary>Builds the real Core services wired like the app, with the default simulation date.</summary>
public static class EngineFactory
{
    public static MessageCatalog Catalog { get; } = new(new HebrewFormatter(), DemoOptions.Default);

    public static ConversationEngine CreateEngine()
    {
        var normalizer = new HebrewTextNormalizer();
        var matcher = new HebrewPrefixMatcher();
        var resolver = new HebrewDateResolver();
        return new ConversationEngine(
            new VisitorIntentDetector(normalizer, matcher),
            new HebrewPreferenceExtractor(normalizer, matcher),
            resolver,
            new RequestValidator(resolver, DemoOptions.Default),
            Catalog,
            DemoOptions.Default);
    }

    public static ConversationSession CreateSession(int contactIndex = 0) => new(DemoContacts.All[contactIndex]);
}
