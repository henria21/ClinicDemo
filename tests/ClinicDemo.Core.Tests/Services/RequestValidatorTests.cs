using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Services;

public class RequestValidatorTests
{
    private readonly RequestValidator _validator = new(new HebrewDateResolver(), DemoOptions.Default);

    private static DateOnly Nov(int day) => new(2026, 11, day);

    private static ReviewSnapshot Snapshot(
        Category category = Category.Checkup,
        DateOnly? day = null,
        TimeWindow? window = null,
        string contactId = "DEMO-A") =>
        new(Guid.NewGuid(), contactId, category,
            new TimePreference(day ?? Nov(24), window ?? new TimeWindow(WindowKind.After, new TimeOnly(16, 0))),
            new[] { "wording" });

    [Fact]
    public void ValidSnapshot_IsValid()
    {
        var result = _validator.Validate(Snapshot());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(16, 0)]
    [InlineData(21, 0)]
    [InlineData(23, 59)]
    public void AnyValidTime_IsAccepted_NoClinicHours(int hour, int minute)
    {
        var window = new TimeWindow(WindowKind.After, new TimeOnly(hour, minute));
        Assert.True(_validator.Validate(Snapshot(window: window)).IsValid);
    }

    [Theory]
    [InlineData(WindowKind.Morning)]
    [InlineData(WindowKind.Midday)]
    [InlineData(WindowKind.Afternoon)]
    public void NamedWindowWithoutTime_IsValid(WindowKind kind)
    {
        Assert.True(_validator.Validate(Snapshot(window: new TimeWindow(kind))).IsValid);
    }

    [Fact]
    public void AfterWithoutTime_IsRejected()
    {
        Assert.False(_validator.Validate(Snapshot(window: new TimeWindow(WindowKind.After))).IsValid);
    }

    [Fact]
    public void NamedWindowWithTime_IsRejected()
    {
        var window = new TimeWindow(WindowKind.Morning, new TimeOnly(9, 0));
        Assert.False(_validator.Validate(Snapshot(window: window)).IsValid);
    }

    [Fact]
    public void UndefinedCategory_IsRejected()
    {
        Assert.False(_validator.Validate(Snapshot(category: (Category)99)).IsValid);
    }

    [Fact]
    public void UndefinedWindowKind_IsRejected()
    {
        Assert.False(_validator.Validate(Snapshot(window: new TimeWindow((WindowKind)99))).IsValid);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(24)]
    [InlineData(28)]
    public void DayInsideRange_IsValid(int day)
    {
        Assert.True(_validator.Validate(Snapshot(day: Nov(day))).IsValid);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(29)]
    [InlineData(15)]
    public void DayOutsideRange_IsRejected(int day)
    {
        Assert.False(_validator.Validate(Snapshot(day: Nov(day))).IsValid);
    }

    [Fact]
    public void NullSnapshot_IsRejected()
    {
        Assert.False(_validator.Validate((ReviewSnapshot?)null).IsValid);
    }

    [Fact]
    public void EmptyReviewId_IsRejected()
    {
        var snapshot = Snapshot() with { ReviewId = Guid.Empty };
        Assert.False(_validator.Validate(snapshot).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankContactId_IsRejected(string contactId)
    {
        Assert.False(_validator.Validate(Snapshot(contactId: contactId)).IsValid);
    }

    [Fact]
    public void MissingTimePreferenceOrWindow_IsRejected()
    {
        Assert.False(_validator.Validate(Snapshot() with { TimePreference = null! }).IsValid);
        Assert.False(_validator.Validate(Snapshot() with { TimePreference = new TimePreference(Nov(24), null!) }).IsValid);
    }

    [Fact]
    public void MultipleProblems_AreAllReported()
    {
        var result = _validator.Validate(Snapshot(category: (Category)99, day: Nov(30)));
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Theory]
    [InlineData(RequestStatus.Reviewed, true)]
    [InlineData(RequestStatus.NeedsClarification, true)]
    [InlineData(RequestStatus.AwaitingReview, false)]
    [InlineData((RequestStatus)99, false)]
    public void StatusChange_OnlyReviewedOrNeedsClarification(RequestStatus target, bool expected)
    {
        Assert.Equal(expected, _validator.ValidateStatusChange(target).IsValid);
    }
}
