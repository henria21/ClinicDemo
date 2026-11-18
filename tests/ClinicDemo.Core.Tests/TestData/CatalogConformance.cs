using System.Text.RegularExpressions;
using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Tests.TestData;

/// <summary>
/// AC-7a helper: checks that a message's text is a render of its own template in the catalog.
/// The template text is matched literally, and each {placeholder} may stand for any non-empty value.
/// </summary>
public static class CatalogConformance
{
    public static bool IsFromCatalog(AssistantMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var template = EngineFactory.Catalog.GetTemplate(message.Id).Text;
        var pattern = "^" + Regex.Replace(Regex.Escape(template), @"\\\{[a-zA-Z]+}", ".+") + "$";
        return Regex.IsMatch(message.Text, pattern);
    }

    public static void AssertAllFromCatalog(IEnumerable<AssistantMessage> messages)
    {
        foreach (var message in messages)
        {
            Assert.True(IsFromCatalog(message), $"Message {message.Id} is not a render of its catalog template.");
        }
    }
}
