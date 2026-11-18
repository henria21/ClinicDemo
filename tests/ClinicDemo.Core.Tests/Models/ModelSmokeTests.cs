using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Tests.Models;

public class ModelSmokeTests
{
    [Fact]
    public void DemoContacts_AreExactlyDemoABC_WithHebrewNames()
    {
        Assert.Equal(new[] { "DEMO-A", "DEMO-B", "DEMO-C" }, DemoContacts.All.Select(c => c.Id));
        Assert.All(DemoContacts.All, c => Assert.Matches(@"\p{IsHebrew}", c.DisplayName));
    }

    [Fact]
    public void RequestStatus_HasExactlyThreeValues_NoneBookingRelated()
    {
        var names = Enum.GetNames<RequestStatus>();
        Assert.Equal(new[] { "AwaitingReview", "Reviewed", "NeedsClarification" }, names);
        Assert.DoesNotContain(names, n =>
            n.Contains("Book", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Confirm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultSimulationDate_Is18Nov2026_AWednesday()
    {
        Assert.Equal(new DateOnly(2026, 11, 18), DemoOptions.Default.SimulationDate);
        Assert.Equal(DayOfWeek.Wednesday, DemoOptions.Default.SimulationDate.DayOfWeek);
    }
}
