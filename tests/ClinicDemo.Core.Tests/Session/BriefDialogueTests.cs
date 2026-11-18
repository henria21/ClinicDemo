using ClinicDemo.Core.Session;
using ClinicDemo.Core.Tests.TestData;

namespace ClinicDemo.Core.Tests.Session;

/// <summary>
/// T14: the dialogue in docs/brief.md must match what the demo actually produces, word for word (AC-1).
/// Consecutive assistant bubbles of one reply (Disclaimer + AskDay) are one line in the brief.
/// </summary>
public class BriefDialogueTests
{
    private const string Visitor = "**מבקר לדוגמה:** ";
    private const string Assistant = "**העוזר:** ";
    private const string SubmitClick = "**[שליחת בקשה]**";

    [Fact]
    public void BriefDialogue_MatchesTheAppsOutput()
    {
        var brief = ReadBriefDialogue();
        var state = EngineFactory.CreateAppState();
        state.SelectContact("DEMO-A");

        foreach (var line in brief)
        {
            if (line == SubmitClick)
            {
                state.Submit(state.Session!.ActiveReview!.ReviewId);
            }
            else if (line.StartsWith(Visitor, StringComparison.Ordinal))
            {
                state.Send(line[Visitor.Length..]);
            }
        }

        var expected = brief.Where(l => l != SubmitClick).ToList();
        Assert.Equal(expected, Render(state.Transcript));
        Assert.Single(state.Requests);
    }

    private static List<string> Render(IReadOnlyList<ChatEntry> transcript)
    {
        var lines = new List<string>();
        var previousWasAssistant = false;
        foreach (var entry in transcript)
        {
            if (entry.FromVisitor)
            {
                lines.Add(Visitor + entry.Text);
            }
            else if (previousWasAssistant)
            {
                lines[^1] += " " + entry.Text;
            }
            else
            {
                lines.Add(Assistant + entry.Text);
            }

            previousWasAssistant = !entry.FromVisitor;
        }

        // A submit is a separate user action, so its message starts a new line even after the summary.
        return SplitSubmitted(lines);
    }

    private static List<string> SplitSubmitted(List<string> lines)
    {
        const string submitted = "בקשת הדמו נשמרה";
        var result = new List<string>();
        foreach (var line in lines)
        {
            var at = line.IndexOf(" " + submitted, StringComparison.Ordinal);
            if (line.StartsWith(Assistant, StringComparison.Ordinal) && at > 0)
            {
                result.Add(line[..at]);
                result.Add(Assistant + line[(at + 1)..]);
            }
            else
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static List<string> ReadBriefDialogue()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ClinicDemo.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var text = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "brief.md"));
        var start = text.IndexOf("<!-- dialogue:start -->", StringComparison.Ordinal);
        var end = text.IndexOf("<!-- dialogue:end -->", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "docs/brief.md must contain the dialogue markers.");

        return text[start..end]
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("<!--", StringComparison.Ordinal))
            .ToList();
    }
}
