using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Services;

public class HebrewDateResolverTests
{
    private static readonly DateOnly Sim = DemoOptions.Default.SimulationDate; // Wed 18.11.2026
    private readonly HebrewDateResolver _resolver = new();

    private static DateOnly Nov(int day) => new(2026, 11, day);

    [Fact]
    public void SimulationDate_IsWednesday18November2026()
    {
        Assert.Equal(Nov(18), Sim);
        Assert.Equal(DayOfWeek.Wednesday, Sim.DayOfWeek);
    }

    [Theory]
    // Weekday + None: nearest on or after today (today counts).
    [InlineData(DayOfWeek.Wednesday, WeekHint.None, 18)]   // רביעי → today
    [InlineData(DayOfWeek.Thursday, WeekHint.None, 19)]    // חמישי → 19.11
    [InlineData(DayOfWeek.Saturday, WeekHint.None, 21)]
    [InlineData(DayOfWeek.Sunday, WeekHint.None, 22)]
    [InlineData(DayOfWeek.Tuesday, WeekHint.None, 24)]
    // Weekday + ThisWeek.
    [InlineData(DayOfWeek.Wednesday, WeekHint.ThisWeek, 18)]
    [InlineData(DayOfWeek.Thursday, WeekHint.ThisWeek, 19)]
    [InlineData(DayOfWeek.Saturday, WeekHint.ThisWeek, 21)]
    // Weekday + NextWeek.
    [InlineData(DayOfWeek.Sunday, WeekHint.NextWeek, 22)]
    [InlineData(DayOfWeek.Tuesday, WeekHint.NextWeek, 24)]  // שלישי + שבוע הבא
    [InlineData(DayOfWeek.Wednesday, WeekHint.NextWeek, 25)]
    [InlineData(DayOfWeek.Thursday, WeekHint.NextWeek, 26)] // חמישי + שבוע הבא → 26.11, not 19.11
    [InlineData(DayOfWeek.Saturday, WeekHint.NextWeek, 28)] // שבת + שבוע הבא → end of range
    public void Weekday_Resolves(DayOfWeek weekday, WeekHint hint, int expectedDay)
    {
        var result = _resolver.Resolve(DayReference.Of(weekday), hint, Sim);

        Assert.Equal(new DateResolution.Resolved(Nov(expectedDay)), result);
    }

    [Fact]
    public void Thursday_HintChangesResult_NextWeek26_None19()
    {
        var thursday = DayReference.Of(DayOfWeek.Thursday);

        Assert.Equal(new DateResolution.Resolved(Nov(26)), _resolver.Resolve(thursday, WeekHint.NextWeek, Sim));
        Assert.Equal(new DateResolution.Resolved(Nov(19)), _resolver.Resolve(thursday, WeekHint.None, Sim));
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]   // ראשון השבוע → already past
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    public void Weekday_ThisWeek_BeforeSimDate_IsOutOfRange(DayOfWeek weekday)
    {
        var result = _resolver.Resolve(DayReference.Of(weekday), WeekHint.ThisWeek, Sim);

        Assert.IsType<DateResolution.OutOfRange>(result);
    }

    [Theory]
    [InlineData(RelativeDay.Today, WeekHint.None, 18)]
    [InlineData(RelativeDay.Tomorrow, WeekHint.None, 19)]          // מחר → 19.11
    [InlineData(RelativeDay.DayAfterTomorrow, WeekHint.None, 20)]
    // A hint that names the week the date already falls in agrees with it → Resolved.
    [InlineData(RelativeDay.Today, WeekHint.ThisWeek, 18)]
    [InlineData(RelativeDay.Tomorrow, WeekHint.ThisWeek, 19)]
    [InlineData(RelativeDay.DayAfterTomorrow, WeekHint.ThisWeek, 20)]
    public void Relative_Resolves(RelativeDay relative, WeekHint hint, int expectedDay)
    {
        var result = _resolver.Resolve(DayReference.Of(relative), hint, Sim);

        Assert.Equal(new DateResolution.Resolved(Nov(expectedDay)), result);
    }

    [Theory]
    [InlineData(RelativeDay.Today)]
    [InlineData(RelativeDay.Tomorrow)]           // מחר + שבוע הבא → Conflict
    [InlineData(RelativeDay.DayAfterTomorrow)]
    public void Relative_WithNextWeekHint_InThisWeek_IsConflict(RelativeDay relative)
    {
        var result = _resolver.Resolve(DayReference.Of(relative), WeekHint.NextWeek, Sim);

        Assert.IsType<DateResolution.Conflict>(result);
    }

    [Fact]
    public void Relative_CrossingIntoNextWeek_MatchesNextWeekHint_ConflictsWithThisWeekHint()
    {
        var saturday = Nov(21);
        var tomorrow = DayReference.Of(RelativeDay.Tomorrow); // Sunday 22.11, next week

        Assert.Equal(new DateResolution.Resolved(Nov(22)), _resolver.Resolve(tomorrow, WeekHint.NextWeek, saturday));
        Assert.IsType<DateResolution.Conflict>(_resolver.Resolve(tomorrow, WeekHint.ThisWeek, saturday));
    }

    [Fact]
    public void RangeHelpers_DefaultSimDate_Are18To28November()
    {
        Assert.Equal(Nov(18), _resolver.FirstDate(Sim));
        Assert.Equal(Nov(28), _resolver.LastDate(Sim));
        Assert.Equal(DayOfWeek.Saturday, _resolver.LastDate(Sim).DayOfWeek);
    }

    [Theory]
    [InlineData(17, false)]
    [InlineData(18, true)]
    [InlineData(23, true)]
    [InlineData(28, true)]
    [InlineData(29, false)]
    public void IsInRange_HonoursBothEnds(int day, bool expected)
    {
        Assert.Equal(expected, _resolver.IsInRange(Nov(day), Sim));
    }

    [Theory]
    [InlineData(15, 11, 28)] // simDate on a Sunday
    [InlineData(21, 11, 28)] // simDate on a Saturday
    [InlineData(22, 12, 5)]  // the following Sunday → range ends Sat 5.12
    public void LastDate_IsSaturdayOfNextWeek(int simDay, int expectedMonth, int expectedDay)
    {
        Assert.Equal(new DateOnly(2026, expectedMonth, expectedDay), _resolver.LastDate(Nov(simDay)));
    }

    [Fact]
    public void EveryResolvedDate_IsInRange()
    {
        var days = Enum.GetValues<DayOfWeek>().Select(DayReference.Of)
            .Concat(Enum.GetValues<RelativeDay>().Select(DayReference.Of))
            .ToList();

        foreach (var sim in Enumerable.Range(15, 7).Select(Nov))
        {
            foreach (var day in days)
            {
                foreach (var hint in Enum.GetValues<WeekHint>())
                {
                    if (_resolver.Resolve(day, hint, sim) is DateResolution.Resolved r)
                    {
                        Assert.True(_resolver.IsInRange(r.Date, sim), $"{day} + {hint} @ {sim} → {r.Date}");
                    }
                }
            }
        }
    }

    [Fact]
    public void MalformedDayReference_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _resolver.Resolve(new DayReference(DayOfWeek.Monday, RelativeDay.Tomorrow), WeekHint.None, Sim));
        Assert.Throws<ArgumentException>(() =>
            _resolver.Resolve(new DayReference(null, null), WeekHint.None, Sim));
    }

    [Fact]
    public void UndefinedHint_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _resolver.Resolve(DayReference.Of(DayOfWeek.Monday), (WeekHint)99, Sim));
    }
}
