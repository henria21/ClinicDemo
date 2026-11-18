using ClinicDemo.Core.Models;
using ClinicDemo.Core.Repositories;
using ClinicDemo.Core.Session;

namespace ClinicDemo.Core.Services;

/// <summary>
/// The conversation state machine (design §5). Pure apart from mutating the session it is given.
/// Every assistant message comes from <see cref="MessageCatalog"/> (NFR-1).
/// </summary>
/// <remarks>
/// One reply asks at most one question, in field order Category → Day → Window: the first conflict or
/// problem found, otherwise the first missing field. When every field is resolved, a new
/// <see cref="ReviewSnapshot"/> becomes the active review.
/// </remarks>
public sealed class ConversationEngine
{
    private const int MissesBeforeForm = 2;

    private readonly VisitorIntentDetector _intents;
    private readonly HebrewPreferenceExtractor _extractor;
    private readonly HebrewDateResolver _resolver;
    private readonly RequestValidator _validator;
    private readonly MessageCatalog _catalog;
    private readonly RequestStore _store;
    private readonly DemoOptions _options;

    public ConversationEngine(
        VisitorIntentDetector intents,
        HebrewPreferenceExtractor extractor,
        HebrewDateResolver resolver,
        RequestValidator validator,
        MessageCatalog catalog,
        RequestStore store,
        DemoOptions options)
    {
        _intents = intents ?? throw new ArgumentNullException(nameof(intents));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Handles one visitor message and returns the assistant's reply (also appended to the session).</summary>
    public IReadOnlyList<AssistantMessage> Handle(ConversationSession session, string text)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(text);

        var replies = HandleCore(session, text);
        return Emit(session, replies);
    }

    /// <summary>
    /// Submits a review (I-1), deciding in this order:
    /// <list type="number">
    /// <item>The store already holds a request for this ReviewId → <see cref="SubmitOutcome.Duplicate"/> with that card. Nothing else changes.</item>
    /// <item>The stage is Reviewing and the ReviewId is the active one → <see cref="SubmitOutcome.Created"/>, stage Submitted, Submitted message.</item>
    /// <item>Otherwise → <see cref="SubmitOutcome.Rejected"/>: a pure no-op. No card, no message, and stage, draft and active review are untouched.</item>
    /// </list>
    /// </summary>
    public SubmitResult Submit(ConversationSession session, Guid reviewId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_store.FindByReviewId(reviewId) is { } existing)
        {
            return new SubmitResult(SubmitOutcome.Duplicate, existing);
        }

        if (session.Stage != ConversationStage.Reviewing || session.ActiveReview is not { } review || review.ReviewId != reviewId)
        {
            return new SubmitResult(SubmitOutcome.Rejected, null);
        }

        var result = _store.Submit(review);
        session.MarkSubmitted(reviewId);
        session.InvalidateReview(); // the submitted review can't be edited or submitted again from the chat
        session.Stage = ConversationStage.Submitted;
        Emit(session, One(TemplateId.Submitted));
        return result;
    }

    /// <summary>Opens the Edit form: invalidates the active review (I-2) and moves to Editing.</summary>
    public void OpenEdit(ConversationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Stage is ConversationStage.Halted or ConversationStage.Submitted)
        {
            throw new InvalidOperationException($"Edit is not available in stage {session.Stage}.");
        }

        session.InvalidateReview();
        session.Stage = ConversationStage.Editing;
    }

    /// <summary>
    /// Applies the Edit / fallback form (FR-14). Invalid values change nothing and the form stays open.
    /// Valid values replace the draft fields and give a fresh summary with a new ReviewId.
    /// </summary>
    public ValidationResult ApplyForm(ConversationSession session, Category category, DayOfWeek weekday, WeekHint week, TimeWindow window)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(window);
        RequireEditing(session);

        var day = DayReference.Of(weekday);
        if (!Enum.IsDefined(week) || !Enum.IsDefined(weekday))
        {
            return ValidationResult.Fail("The day or week is not a valid value.");
        }

        if (_resolver.Resolve(day, week, _options.SimulationDate) is not DateResolution.Resolved resolved)
        {
            return ValidationResult.Fail("The day is outside the selectable range.");
        }

        var validation = _validator.Validate(category, new TimePreference(resolved.Date, window));
        if (!validation.IsValid)
        {
            return validation;
        }

        var draft = session.Draft;
        draft.Category = category;
        draft.DayReference = day;
        draft.WeekHint = week;
        draft.Window = window;
        session.ResetMisses();
        Emit(session, NextStep(session));
        return ValidationResult.Ok;
    }

    /// <summary>Closes the form without changes: a fresh summary of the unchanged draft (new ReviewId, I-4), or the next question if a field is missing.</summary>
    public IReadOnlyList<AssistantMessage> CancelEdit(ConversationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireEditing(session);

        session.Stage = ConversationStage.Collecting;
        return Emit(session, NextStep(session));
    }

    private static void RequireEditing(ConversationSession session)
    {
        if (session.Stage != ConversationStage.Editing)
        {
            throw new InvalidOperationException($"The form is not open (stage {session.Stage}).");
        }
    }

    private List<AssistantMessage> HandleCore(ConversationSession session, string text)
    {
        var intent = _intents.Detect(text);

        // 1. Medical, in any stage: halt and discard the draft so no health text is kept (FR-12).
        if (intent == VisitorIntent.Medical)
        {
            session.InvalidateReview();
            session.DiscardDraft();
            session.Stage = ConversationStage.Halted;
            return One(TemplateId.MedicalHandoff);
        }

        // 2. Halted: only an explicit restart leaves it.
        if (session.Stage == ConversationStage.Halted)
        {
            return One(TemplateId.RestartRequired);
        }

        // 3. Booking questions and demands change no data, so an active review stays active (FR-11, FR-13).
        if (intent is VisitorIntent.AsksIfBooked or VisitorIntent.DemandsConfirmation)
        {
            return One(session.Stage == ConversationStage.Submitted
                ? TemplateId.NotABookingAfterSubmit
                : TemplateId.NotABookingBeforeSubmit);
        }

        // 4. Already submitted.
        if (session.Stage == ConversationStage.Submitted)
        {
            return One(TemplateId.AlreadySent);
        }

        // 5. Preference.
        return HandlePreference(session, text);
    }

    private List<AssistantMessage> HandlePreference(ConversationSession session, string text)
    {
        var wasReviewing = session.Stage == ConversationStage.Reviewing;
        if (wasReviewing)
        {
            session.InvalidateReview(); // I-2
        }

        session.Stage = ConversationStage.Collecting;
        var draft = session.Draft;
        draft.OriginalWording.Add(text);

        var result = _extractor.Extract(text);
        var understood = result.Categories.Count > 0 || result.Days.Count > 0 || result.Windows.Count > 0
            || result.WeekHint != WeekHint.None || result.WeekHintConflict || result.AmbiguousTime;

        if (!understood)
        {
            return NotUnderstood(session, wasReviewing);
        }

        session.ResetMisses();
        AssistantMessage? categoryIssue = ApplyCategory(draft, result);
        AssistantMessage? dayIssue = ApplyDay(draft, result);
        AssistantMessage? windowIssue = ApplyWindow(draft, result);

        var issue = categoryIssue ?? dayIssue ?? windowIssue;
        if (issue is not null)
        {
            return new List<AssistantMessage> { issue };
        }

        return NextStep(session);
    }

    private AssistantMessage? ApplyCategory(RequestDraft draft, ExtractionResult result)
    {
        if (result.Categories.Count > 1)
        {
            draft.Category = null; // I-3
            return _catalog.Render(TemplateId.ClarifyCategory, new MessageArgs { Categories = result.Categories });
        }

        if (result.Categories.Count == 1)
        {
            draft.Category = result.Categories[0];
        }

        return null;
    }

    private AssistantMessage? ApplyDay(RequestDraft draft, ExtractionResult result)
    {
        if (result.WeekHintConflict)
        {
            draft.DayReference = null; // I-3; the stored WeekHint is left as it was.
            return _catalog.Render(TemplateId.ClarifyDay, new MessageArgs
            {
                Days = result.Days,
                Weeks = new[] { WeekHint.ThisWeek, WeekHint.NextWeek },
            });
        }

        if (result.Days.Count > 1)
        {
            draft.DayReference = null; // I-3
            return _catalog.Render(TemplateId.ClarifyDay, new MessageArgs { Days = result.Days });
        }

        if (result.WeekHint != WeekHint.None)
        {
            draft.WeekHint = result.WeekHint; // persists until a newer hint replaces it
        }

        if (result.Days.Count == 1)
        {
            draft.DayReference = result.Days[0];
        }

        // Re-resolve on every change, including a change to the hint alone.
        if (draft.DayReference is null)
        {
            return null;
        }

        switch (_resolver.Resolve(draft.DayReference, draft.WeekHint, _options.SimulationDate))
        {
            case DateResolution.OutOfRange:
                draft.DayReference = null;
                draft.WeekHint = WeekHint.None; // so the visitor's next answer isn't trapped by the same hint
                return _catalog.Render(TemplateId.DayOutOfRange, new MessageArgs
                {
                    LastDate = _resolver.LastDate(_options.SimulationDate),
                });

            case DateResolution.Conflict:
                var conflictingDay = draft.DayReference;
                var conflictingWeek = draft.WeekHint;
                draft.DayReference = null;
                draft.WeekHint = WeekHint.None; // the visitor's answer decides; neither part is kept
                return _catalog.Render(TemplateId.ClarifyDay, new MessageArgs
                {
                    Days = new[] { conflictingDay },
                    Weeks = new[] { conflictingWeek },
                });

            default:
                return null;
        }
    }

    private AssistantMessage? ApplyWindow(RequestDraft draft, ExtractionResult result)
    {
        TimeWindow? window = null;
        if (result.Windows.Count > 0 && !TryCombineWindows(result.Windows, out window))
        {
            draft.Window = null; // I-3
            return _catalog.Render(TemplateId.ClarifyWindow, new MessageArgs { Windows = result.Windows });
        }

        // An ambiguous time is never guessed, even next to a day part ("בבוקר אחרי 4").
        if (result.AmbiguousTime)
        {
            draft.Window = null;
            return _catalog.Render(TemplateId.AskTime24h);
        }

        if (window is not null)
        {
            draft.Window = window;
        }

        return null;
    }

    /// <summary>
    /// Combines the windows of one message. A parsing rule, not clinic hours: Morning means before 12:00 and
    /// Midday/Afternoon mean 12:00 or later. One day part plus a compatible "after" time gives the time
    /// (more specific); two day parts, two times, or an incompatible pair is a conflict.
    /// </summary>
    private static bool TryCombineWindows(IReadOnlyList<TimeWindow> windows, out TimeWindow window)
    {
        var parts = windows.Where(w => w.Kind != WindowKind.After).ToList();
        var afters = windows.Where(w => w.Kind == WindowKind.After).ToList();
        window = windows[0];

        if (parts.Count > 1 || afters.Count > 1)
        {
            return false;
        }

        if (parts.Count == 1 && afters.Count == 1)
        {
            var noon = new TimeOnly(12, 0);
            var after = afters[0].After!.Value;
            var compatible = parts[0].Kind == WindowKind.Morning ? after < noon : after >= noon;
            if (!compatible)
            {
                return false;
            }

            window = afters[0];
        }

        return true;
    }

    private List<AssistantMessage> NotUnderstood(ConversationSession session, bool wasReviewing)
    {
        var draft = session.Draft;
        if (wasReviewing && draft.Category is not null && draft.DayReference is not null && draft.Window is not null)
        {
            var summary = NextStep(session);
            summary.Insert(0, _catalog.Render(TemplateId.NoChangeDetected));
            return summary;
        }

        int misses;
        if (draft.Category is null)
        {
            misses = ++session.CategoryMisses;
        }
        else if (draft.DayReference is null)
        {
            misses = ++session.DayMisses;
        }
        else
        {
            misses = ++session.WindowMisses;
        }

        if (misses >= MissesBeforeForm)
        {
            session.ResetMisses();
            session.Stage = ConversationStage.Editing; // FR-14 fallback form
            return One(TemplateId.OfferForm);
        }

        return NextStep(session);
    }

    /// <summary>Asks for the next missing field, or creates a fresh review when every field is resolved.</summary>
    private List<AssistantMessage> NextStep(ConversationSession session)
    {
        var draft = session.Draft;
        if (draft.Category is null)
        {
            return One(TemplateId.AskCategory);
        }

        if (draft.DayReference is null)
        {
            return One(TemplateId.AskDay);
        }

        if (draft.Window is null)
        {
            return One(TemplateId.AskWindow);
        }

        if (_resolver.Resolve(draft.DayReference, draft.WeekHint, _options.SimulationDate)
            is not DateResolution.Resolved resolved)
        {
            // Unreachable: ApplyDay nulls a day that doesn't resolve.
            draft.DayReference = null;
            return One(TemplateId.AskDay);
        }

        var preference = new TimePreference(resolved.Date, draft.Window);
        var snapshot = new ReviewSnapshot(
            Guid.NewGuid(), // I-4: a new ReviewId for every summary
            session.Contact.Id,
            draft.Category.Value,
            preference,
            draft.OriginalWording.ToList());

        var validation = _validator.Validate(snapshot);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("The engine built an invalid review: " + string.Join("; ", validation.Errors));
        }

        session.SetActiveReview(snapshot);
        session.Stage = ConversationStage.Reviewing;
        return new List<AssistantMessage>
        {
            _catalog.Render(TemplateId.Summary, new MessageArgs
            {
                Category = snapshot.Category,
                Time = preference,
                Contact = session.Contact,
            }),
        };
    }

    private List<AssistantMessage> One(TemplateId id) => new() { _catalog.Render(id) };

    /// <summary>Prefixes the first reply of a session with the Disclaimer and records the reply.</summary>
    private IReadOnlyList<AssistantMessage> Emit(ConversationSession session, List<AssistantMessage> replies)
    {
        if (!session.DisclaimerShown)
        {
            replies.Insert(0, _catalog.Render(TemplateId.Disclaimer));
            session.DisclaimerShown = true;
        }

        session.AddMessages(replies);
        return replies;
    }
}
