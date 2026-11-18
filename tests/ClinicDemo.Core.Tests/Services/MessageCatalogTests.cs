using System.Text;
using ClinicDemo.Core.Models;
using ClinicDemo.Core.Services;

namespace ClinicDemo.Core.Tests.Services;

public class MessageCatalogTests
{
    private const string ApprovedFileName = "MessageCatalog.approved.txt";

    private readonly MessageCatalog _catalog = new(new HebrewFormatter(), DemoOptions.Default);

    /// <summary>Fixed sample args used for the approved file. Changing them requires re-approval.</summary>
    private static readonly MessageArgs SampleArgs = new()
    {
        Categories = new[] { Category.Checkup, Category.Consultation },
        Days = new[] { DayReference.Of(DayOfWeek.Tuesday), DayReference.Of(DayOfWeek.Wednesday) },
        Windows = new[] { new TimeWindow(WindowKind.Morning), new TimeWindow(WindowKind.After, new TimeOnly(16, 0)) },
        LastDate = new DateOnly(2026, 11, 28),
        Category = Category.Checkup,
        Time = new TimePreference(new DateOnly(2026, 11, 24), new TimeWindow(WindowKind.After, new TimeOnly(16, 0))),
        Contact = DemoContacts.All[0],
    };

    [Fact]
    public void EveryTemplateId_HasATemplate()
    {
        foreach (var id in Enum.GetValues<TemplateId>())
        {
            var template = _catalog.GetTemplate(id);
            Assert.Equal(id, template.Id);
            Assert.False(string.IsNullOrWhiteSpace(template.Text));
            Assert.False(string.IsNullOrWhiteSpace(template.Asserts));
        }

        Assert.Equal(Enum.GetValues<TemplateId>(), _catalog.Templates.Select(t => t.Id));
    }

    [Fact]
    public void EveryTemplate_RendersWithSampleArgs_WithNoPlaceholderLeft()
    {
        foreach (var id in Enum.GetValues<TemplateId>())
        {
            var message = _catalog.Render(id, SampleArgs);
            Assert.Equal(id, message.Id);
            Assert.DoesNotContain("{", message.Text);
            Assert.DoesNotContain("}", message.Text);
        }
    }

    /// <summary>AC-7b: the rendered catalog plus asserts notes must equal the reviewed approved file.</summary>
    [Fact]
    public void Catalog_MatchesApprovedFile()
    {
        var received = BuildApprovalText();
        var approvedPath = Path.Combine(FindSolutionRoot(), "tests", "ClinicDemo.Core.Tests", "Approved", ApprovedFileName);
        var approved = File.Exists(approvedPath) ? File.ReadAllText(approvedPath, Encoding.UTF8) : string.Empty;

        if (Normalize(approved) != Normalize(received))
        {
            var receivedPath = Path.Combine(AppContext.BaseDirectory, "MessageCatalog.received.txt");
            File.WriteAllText(receivedPath, received, new UTF8Encoding(false));
            Assert.Fail($"MessageCatalog differs from {approvedPath}. Review {receivedPath} and, once Henri approves the Hebrew, copy it over the approved file.");
        }
    }

    /// <summary>AC-7c: the post-submit message and the reply to "זה נקבע?" state it is not a booking.</summary>
    [Fact]
    public void Submitted_StatesItIsNotABooking()
    {
        var text = _catalog.Render(TemplateId.Submitted).Text;
        Assert.Contains("זה אינו אישור תור", text);
        Assert.Contains("לא נשלחה הודעה למרפאה אמיתית", text);
    }

    [Fact]
    public void NotABookingAfterSubmit_StatesItIsNotABooking()
    {
        var text = _catalog.Render(TemplateId.NotABookingAfterSubmit).Text;
        Assert.Contains("התור לא נקבע", text);
        Assert.Contains("נשלחה רק בקשה", text);
    }

    [Fact]
    public void Summary_ReproducesAppendixA()
    {
        var text = _catalog.Render(TemplateId.Summary, SampleArgs).Text;
        Assert.Equal(
            "לסיכום: בקשה לבדיקה, ביום שלישי בשבוע הבא אחרי 16:00, עבור איש קשר לדוגמה א׳. לשלוח את הבקשה לבדיקה של צוות הדמו?",
            text);
    }

    [Fact]
    public void DisclaimerThenAskDay_ReproducesAppendixAFirstReply()
    {
        var text = _catalog.Render(TemplateId.Disclaimer).Text + " " + _catalog.Render(TemplateId.AskDay).Text;
        Assert.Equal("זהו דמו בלבד. אין להזין פרטים אישיים או רפואיים. באיזה יום מועדף לך להגיע?", text);
    }

    [Theory]
    [InlineData(TemplateId.ClarifyCategory)]
    [InlineData(TemplateId.ClarifyDay)]
    [InlineData(TemplateId.ClarifyWindow)]
    [InlineData(TemplateId.DayOutOfRange)]
    [InlineData(TemplateId.Summary)]
    public void TemplatesWithPlaceholders_ThrowWhenArgsMissing(TemplateId id)
    {
        Assert.Throws<ArgumentException>(() => _catalog.Render(id));
    }

    private string BuildApprovalText()
    {
        var sb = new StringBuilder();
        sb.Append("# MessageCatalog v1: approved assistant text. Henri reviews the Hebrew; any change must be re-approved.\n");
        sb.Append("# Simulation date: 18.11.2026. Sample args: categories=[Checkup, Consultation], days=[Tuesday, Wednesday],\n");
        sb.Append("# windows=[Morning, After 16:00], lastDate=28.11.2026, category=Checkup, time=24.11.2026 After 16:00, contact=DEMO-A.\n");
        foreach (var template in _catalog.Templates)
        {
            sb.Append('\n');
            sb.Append("## ").Append(template.Id).Append('\n');
            sb.Append("Asserts: ").Append(template.Asserts).Append('\n');
            sb.Append("Template: ").Append(template.Text).Append('\n');
            sb.Append("Rendered: ").Append(_catalog.Render(template.Id, SampleArgs).Text).Append('\n');
        }

        return sb.ToString();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n").TrimStart('﻿');

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ClinicDemo.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
