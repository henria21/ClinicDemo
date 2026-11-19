# Design — Hebrew Clinic Appointment-Request Demo (v1)

**Mode:** sales demo. Implements the approved `requirements.md`.

## 1. Requirements recap

- One RTL page with a Hebrew chat pane and a receptionist inbox pane, using fictional contacts and a fixed simulation date.
- The assistant collects **category + day/time window**, clarifies missing or conflicting values, and shows a review summary with Edit / Submit. Submitting creates one card ("ממתין לבדיקת צוות") that staff mark "נבדק" or "דורש הבהרה".
- Nothing is ever booked. No AI and no external calls in v1.

## 2. Key decision: stack

**Chosen: Blazor WebAssembly (standalone, front-end only) + a plain C# class library for all logic.**

| Option | For | Against |
|---|---|---|
| **Blazor WASM + C# Core library** ✅ | C# throughout. Front-end only, as the requirements allow. No server, so Reset is trivial (FR-18). Logic is unit-testable with xUnit. v2 can add an ASP.NET Core API that reuses the same Core library unchanged. | ~2–3 MB first load (fine for a demo). Blazor is less familiar to clinic-facing web devs than React (irrelevant here). |
| React + TypeScript, front-end only | Small bundle, familiar React | Logic in TypeScript, not C#. A second test toolchain. |
| React + ASP.NET Core API | Henri's day-job stack | A server with no job in v1. Reset must clear two places. More moving parts for a 90-second demo. |

The design below assumes Blazor WASM. If you prefer React, the Core design carries over one-to-one into TypeScript modules.

## 3. Architecture

```
┌──────────────────────── Browser (single tab) ────────────────────────┐
│  ClinicDemo.Web (Blazor WASM — UI only)                              │
│   Home.razor ── ChatPane ──────────┐        InboxPane ── RequestCard │
│        │         ContactPicker     │            ▲                    │
│        │         ReviewSummary     │            │ Changed event      │
│        │         PreferenceForm    ▼            │                    │
│        └──────────────► DemoAppState ───────────┘                    │
│                           │            │                             │
│  ClinicDemo.Core (pure C#, no UI, no I/O)                            │
│                           ▼            ▼                             │
│              ConversationEngine    RequestStore (in-memory)          │
│               │  │  │  │                │                            │
│   VisitorIntentDetector  HebrewPreferenceExtractor  RequestValidator │
│   HebrewDateResolver     HebrewTextNormalizer       MessageCatalog   │
└──────────────────────────────────────────────────────────────────────┘
          No network calls after the app's own static files load.
```

- **Core** holds all behaviour: dialogue, Hebrew parsing, validation, the store, the message templates. It is deterministic and synchronous, and it doesn't know Blazor exists.
- **Web** renders state and forwards user actions. Components contain no business rules.
- **Few interfaces (NFR-8).** Services are concrete classes registered in DI. There is no `IRequestStore` or `IExtractor` "for later". The only abstraction is the simulation date, passed in as `DemoOptions`, so tests and the demo agree on "today".

## 4. OOD design

### Models (`Core/Models`)

| Type | Kind | Purpose |
|---|---|---|
| `DemoContact` | record `(string Id, string DisplayName)` | e.g. `("DEMO-A", "איש קשר לדוגמה א׳")` |
| `DemoContacts` | static class | The fixed list A/B/C (FR-4) |
| `Category` | enum | `Checkup` (בדיקה), `FollowUp` (ביקור מעקב), `Consultation` (ייעוץ) |
| `WindowKind` | enum | `Morning`, `Midday`, `Afternoon`, `After` |
| `TimeWindow` | record `(WindowKind Kind, TimeOnly? After)` | `After` is set only for `WindowKind.After` (e.g. 16:00). Always an unambiguous 24-hour time. |
| `TimePreference` | record `(DateOnly Day, TimeWindow Window)` | The resolved preference |
| `DayReference` | record `(DayOfWeek? Weekday, RelativeDay? Relative)` | What the visitor actually said about the day ("שלישי", or "מחר"). Exactly one of the two is set. |
| `RelativeDay` | enum | `Today`, `Tomorrow`, `DayAfterTomorrow` |
| `RequestDraft` | class | Mutable during a conversation: `Category?`, `DayReference?`, **`WeekHint WeekHint`** (default `None`), `TimeWindow? Window`, `List<string> OriginalWording`. The **resolved date is never stored**. It is recomputed from `DayReference` + `WeekHint` + simDate each time the draft changes, so a later change to either part re-resolves correctly. |
| `ReviewSnapshot` | record `(Guid ReviewId, string ContactId, Category, TimePreference, IReadOnlyList<string> Wording)` | Immutable copy shown on the summary. Its `ReviewId` is the idempotency key. |
| `SubmitResult` | record `(SubmitOutcome Outcome, AppointmentRequest? Request)` | Three outcomes: <br>• `Created`: the active review was submitted. <br>• `Duplicate`: this ReviewId **was submitted before** → returns that existing card. <br>• `Rejected`: the ReviewId was never submitted and isn't the active one (stale after an edit, a conflict or a handoff, or unknown) → `Request = null`, **no card, and no change to the session or the active review**. |
| `AppointmentRequest` | record | `Id` ("REQ-0001"), `ContactId`, `Category`, `TimePreference`, `OriginalWording`, `Status`, `ReviewId` |
| `RequestStatus` | enum | `AwaitingReview`, `Reviewed`, `NeedsClarification`. **No booked/confirmed value exists.** |
| `ConversationStage` | enum | `Collecting`, `Reviewing`, `Editing` (form open, for Edit or fallback), `Submitted`, `Halted` (after a medical handoff; only an explicit restart leaves it) |
| `VisitorIntent` | enum | `Preference`, `Medical`, `AsksIfBooked`, `DemandsConfirmation` |
| `ExtractionResult` | record | Candidate sets: `Categories`, `Days`, `Windows`, `WeekHint` (`None` / `ThisWeek` / `NextWeek`), `WeekHintConflict` (the message names both weeks, e.g. "השבוע או בשבוע הבא"; `WeekHint` is then `None`), and `UnknownDayName` for day-like words that weren't recognised |
| `TemplateId` | enum | One value per assistant message (see MessageCatalog) |
| `AssistantMessage` | record `(TemplateId Id, string Text)` | What the chat shows |
| `DemoOptions` | record `(DateOnly SimulationDate)` | Default 2026-11-18 (Wednesday) |

### Services (`Core/Services`): all stateless, registered as singletons

| Class | Responsibility |
|---|---|
| `HebrewTextNormalizer` | Strips niqqud. Unifies geresh/gershayim (`׳ ' ״ "`) and maps final letters, for matching only. **Removes punctuation except a colon between digits**, so `16:00` stays one token; a sentence-final `16:00.` loses only the dot. Splits into tokens. |
| `HebrewPrefixMatcher` | Finds a known keyword inside a token. It tries, in order: <br>1. the bare token (`בוקר`) <br>2. one prefix from {ב, ל, מ, ה, כ, ו, ש} (`לבדיקה`, `בבוקר`) <br>3. a conjunction {ו, ש} followed by one of {ב, ל, מ, ה, כ} (`וביום`, `ובבוקר`, `שבשבוע`) <br>A match counts only if the remainder is a dictionary keyword. Otherwise the token is unknown, so no partial or guessed matches. |
| `HebrewPreferenceExtractor` | Turns tokens into an `ExtractionResult` using keyword dictionaries: categories, day names (`ראשון`..`שבת`, `יום ג׳`), relative days (`היום`, `מחר`, `מחרתיים`), week hints (`השבוע`, `שבוע הבא`), day parts. <br>**Times are 24-hour only:** `אחרי HH:MM` (00–23 : 00–59), or a bare number 13–23 (`אחרי 16`). <br>A bare 0–12 (`אחרי 4`), a number word (`אחרי ארבע`) or anything else after `אחרי` gives `AmbiguousTime`: the window stays unresolved and the engine asks `AskTime24h`. **Nothing is silently read as PM.** |
| `HebrewDateResolver` | `(DayReference, WeekHint, simDate) → DateResolution` (`Resolved(date)`, `OutOfRange`, or `Conflict`). Weeks run Sunday–Saturday. Range: simDate through the end of next week (18.11–28.11). Rules: <br>• Weekday + `None` → the nearest occurrence on or after simDate. <br>• Weekday + `ThisWeek` → that day in the current week; a day before simDate → `OutOfRange`. <br>• Weekday + `NextWeek` → that day in the next week (**שבוע הבא + חמישי → 26.11, not 19.11**). <br>• Relative day + `None` → simDate + n. <br>• Relative day + a hint for a different week (e.g. "מחר" with `NextWeek`) → `Conflict`. |
| `VisitorIntentDetector` | Returns a `VisitorIntent`. `Medical` comes from a **small, documented keyword list** that matches the synthetic demo examples only (FR-12 limitation). `AsksIfBooked` matches "נקבע?", "זה סגור", "יש לי תור"; `DemandsConfirmation` matches "תאשר", "תקבע לי", "תתעלם מ...". Order: Medical first, then the others, then Preference. |
| `RequestValidator` | Checks a draft before review: a defined `Category`, a day inside the range, a window that is well-formed (any valid `TimeOnly` for `After`). **There are no clinic hours in the demo.** Any time is accepted as a stated preference, and no time is treated as available or unavailable. Checks status changes: the target must be `Reviewed` or `NeedsClarification`. Returns `ValidationResult` for user flows. The store throws a typed exception when a call bypasses validation. |
| `MessageCatalog` | **The only source of visitor-facing assistant text (NFR-1).** It maps `TemplateId` to a Hebrew template plus a one-line reviewed "what this asserts" note. `Render(TemplateId, args)` fills in only formatted, validated values. |
| `HebrewFormatter` | Formats category labels, `TimePreference` → "ביום שלישי בשבוע הבא אחרי 16:00", and card dates "יום ג׳ 24.11". |
| `ConversationEngine` | The state machine (below). `Handle(ConversationSession, string text) → IReadOnlyList<AssistantMessage>`, plus `ApplyForm(...)` and `Submit(...)`. Pure apart from mutating the session it is given. |

### Session and store

| Class | Lifetime | Responsibility |
|---|---|---|
| `ConversationSession` (`Core/Session`) | One per chat | Holds the `DemoContact`, `Stage`, `RequestDraft`, `Messages`, **`ActiveReview` (`ReviewSnapshot?`)**, the set of submitted ReviewIds, the `DisclaimerShown` flag, and a not-understood counter per field. `InvalidateReview()` sets `ActiveReview = null` and is the only way to clear it. |
| `RequestStore` (`Core/Repositories`) | Singleton | Concrete, in-memory class with `Submit(ReviewSnapshot)`, `SetStatus(id, status)`, `GetAll()`, `Clear()`, and an `event Action Changed`. Idempotent: it keeps a `Dictionary<Guid, AppointmentRequest>` keyed by ReviewId. A lock guards the sequence counter (see §8). |
| `DemoAppState` (`Core/Session`) | Singleton | The facade the UI talks to. Owns the current session and the store. `SelectContact`, `Send`, `OpenEdit`, `ApplyForm`, `CancelEdit`, `Submit`, `RestartConversation` (new chat, inbox kept), `SetStatus`, `Reset` (everything). Raises `Changed` after every action. Also keeps the chat **`Transcript`** (`ChatEntry` list: visitor and assistant bubbles; a Summary bubble carries its ReviewId so the UI can apply I-5), cleared on restart, reset and a new contact. In memory only. |

**DI.** `ClinicDemo.Web/ClinicDemoServices.AddClinicDemo()` registers every service, the store and `DemoAppState` as singletons (one per browser tab in WASM; a refresh resets). No `HttpClient` is registered.

### Exceptions (`Core/Exceptions`)

- `InvalidRequestException`: `Submit` received a snapshot that fails validation. This can't happen through the UI.
- `InvalidStatusChangeException`: an unknown request ID or a disallowed target status (AC-10).

### Patterns used

- **Facade:** `DemoAppState`, so the UI makes one call per user action.
- **State machine:** `ConversationEngine` over `ConversationStage`.
- **Repository:** `RequestStore` (in-memory).
- **Template catalog:** `MessageCatalog`. Closed, reviewable text is what makes the no-booking guarantee hold.
- **Observer:** the `Changed` event re-renders the inbox when the chat submits.
- **Singleton via DI:** the stateless services and the store.

## 5. Conversation state machine

```
[contact picked] ─► Collecting ──all fields resolved──► Reviewing ──Submit──► Submitted
                      ▲    │                              │   │
                      │    │◄── any change / conflict /───┘   │ Edit
                      │    │    out-of-range / no-change      ▼
                      │    └── 2× not understood ──────► Editing ──save──► Reviewing (fresh ReviewId)
                      │                                       └─cancel─► Reviewing (fresh ReviewId)
   any stage ── medical input ──► Halted ── "שיחה חדשה" (RestartConversation) ──► [contact picker]
   Submitted ── "שיחה חדשה" (RestartConversation) ──► [contact picker], inbox kept
   any stage ── Reset demo ─────► [contact picker], inbox cleared
```

### Review invariants

These are enforced in `ConversationEngine` and tested directly:

- **I-1** `Submit(reviewId)` decides in this order:
  1. The store already holds a request for this ReviewId → `Duplicate` with that card. Nothing else changes.
  2. `Stage == Reviewing` and the ReviewId equals `ActiveReview.ReviewId` → `Created`.
  3. Otherwise → `Rejected`. **This is a pure no-op:** no card, no message, and the stage, draft and active review are untouched. A stale Submit can't knock out the fresh summary the visitor is looking at.
- **I-2** Every path out of `Reviewing` other than a successful Submit calls `InvalidateReview()` first. That covers any preference message (changed, conflicting, out of range, or not understood), opening Edit, medical input, restart, and Reset. The only replies that leave the review active are the pure `AsksIfBooked` / `DemandsConfirmation` answers, which change no data.
- **I-3** A field involved in a conflict or an out-of-range value is set to **unresolved** (null) in the draft, not left at its old value. A fresh summary appears only once every field is resolved again.
- **I-4** Each summary gets a new ReviewId, even when the contents are the same as before.
- **I-5** In the UI, only the bubble for `ActiveReview` renders Edit / Submit. Earlier summary bubbles render as plain text, so no stale button exists.

### `Handle(text)` per message

1. **Medical** (any stage) → `MedicalHandoff` only.
   - `InvalidateReview()`, **discard the draft**, including `OriginalWording`, so no health text is kept. Stage → `Halted`.
   - No follow-up question (FR-12).
2. **Stage `Halted`** → every further message gets `RestartRequired`. The chat shows a **"שיחה חדשה"** button that calls `RestartConversation()`: back to contact picker, inbox kept. Submit is impossible because there is no active review (I-1).
3. **AsksIfBooked / DemandsConfirmation** → `NotABookingBeforeSubmit` or `NotABookingAfterSubmit`, depending on stage (FR-11, FR-13). No data changes, so an active review stays active.
4. **Stage `Submitted`** and anything else → `AlreadySent`.
5. **Preference.** If `Stage == Reviewing`: `InvalidateReview()` and stage → `Collecting` (I-2). Then:
   - Append the text to `OriginalWording` and extract.
   - **Week hint.** If the message has one, set `draft.WeekHint`. It **persists** across later messages and edits until a newer hint replaces it.
   - **Conflicts**, checked per field. Each conflicting field is set to null (I-3):
     - two or more categories → `ClarifyCategory(options)`
     - two or more days, **or both weeks named in one message (`WeekHintConflict`)** → `ClarifyDay(options)`. A week-hint conflict leaves the stored `draft.WeekHint` unchanged and nulls the day.
     - incompatible windows → `ClarifyWindow(options)`. This is a **parsing rule, not clinic hours**: `Morning` means before 12:00 and `Midday`/`Afternoon` mean 12:00 or later, so `Morning` + `After ≥ 12:00` and `Midday`/`Afternoon` + `After < 12:00` conflict.
   - **Ambiguous time** (`אחרי 4`) → window set to null → `AskTime24h`.
   - **Single values** overwrite the draft field (FR-9: "בעצם עדיף רביעי").
   - **Re-resolve the day** from `DayReference` + `WeekHint` on every change, including a change to the hint alone ("בעצם בשבוע הזה").
     - `OutOfRange` → day set to null → `DayOutOfRange`
     - `Conflict` → day set to null → `ClarifyDay`, listing the day and the week ("מחר או בשבוע הבא")
     - In both cases the stored `WeekHint` is also reset to `None`, so the visitor's next answer isn't resolved against the same hint again.
     - `ClarifyDay`'s `{days}` can list week options after the days (`MessageArgs.Weeks`), for a week conflict or a day/week conflict. The template text is unchanged.
   - **One question per reply**, in field order Category → Day → Window: the first conflict or problem, otherwise the first missing field. An ambiguous time is never combined with a day part ("בבוקר אחרי 4" → `AskTime24h`). One day part plus a compatible `After` time ("אחר הצהריים אחרי 16:00") keeps the time.
   - **No change understood**: if every field is already resolved (the visitor was reviewing) → `NoChangeDetected` + a fresh `Summary` with a new ReviewId. Otherwise, increment the missing field's counter; at 2 → `OfferForm`, stage `Editing` (FR-14).
   - **Next unresolved field**, in order Category → Day → Window → its question (`AskCategory` / `AskDay` / `AskWindow`).
   - **All fields resolved and valid** → a new `ReviewSnapshot` (new ReviewId) → `ActiveReview`, stage `Reviewing`, `Summary`.
6. The **first** assistant reply in a session is prefixed with `Disclaimer`. That reproduces Appendix A exactly.

**Edit.** `OpenEdit()` → `InvalidateReview()`, stage `Editing`.
- The form is pre-filled: category, a **weekday + week selector** (השבוע / שבוע הבא, defaulting to `draft.WeekHint`, so the context survives the edit), and a window.
- `ApplyForm` validates, then gives a fresh summary.
- `CancelEdit` gives a fresh summary of the unchanged draft (new ReviewId, I-4).

**Submit(reviewId)** follows I-1. On `Created` → stage `Submitted` → `Submitted` template, and `ActiveReview` is cleared so no button can submit it again. Step 1 uses `RequestStore.FindByReviewId`, so checking "already submitted?" never creates a card. The engine receives the `RequestStore` through its constructor.

**Engine API (T9).** `Handle(session, text)`, `Submit(session, reviewId) → SubmitResult`, `OpenEdit(session)`, `ApplyForm(session, category, weekday, week, window) → ValidationResult`, `CancelEdit(session)`. Invalid form values change nothing and the form stays open. `OpenEdit` after Submitted or Halted, and form actions outside `Editing`, throw `InvalidOperationException` (the UI never offers them). The form sets no wording: the card's original wording stays the chat messages.

### MessageCatalog, v1

| TemplateId | Hebrew text (args in braces) | Asserts |
|---|---|---|
| Disclaimer | זהו דמו בלבד. אין להזין פרטים אישיים או רפואיים. | Demo only, no personal data |
| AskCategory | איזה סוג תור מבוקש: בדיקה, ביקור מעקב או ייעוץ? | Question |
| AskDay | באיזה יום מועדף לך להגיע? | Question |
| AskWindow | באיזה חלק של היום מועדף לך: בוקר, צהריים או אחר הצהריים? | Question |
| ClarifyCategory | הוזכרו כמה סוגי תור: {categories}. איזה מהם מבוקש? | Question |
| ClarifyDay | הוזכרו כמה ימים: {days}. איזה יום מועדף לך? | Question |
| ClarifyWindow | ההעדפות לשעה לא מתאימות זו לזו: {windows}. מה עדיף לך? | Question |
| AskTime24h | כדי לא לטעות בשעה, אפשר לכתוב אותה בפורמט 24 שעות, למשל 16:00? | Question |
| DayOutOfRange | אפשר לבחור יום מהיום ועד סוף השבוע הבא ({lastDate}). איזה יום מועדף לך? | Range limit, question |
| Summary | לסיכום: בקשה ל{category}, {time}, עבור {contact}. לשלוח את הבקשה לבדיקה של צוות הדמו? | Restates the request, asks to send |
| Submitted | בקשת הדמו נשמרה וממתינה לבדיקת הצוות. זה אינו אישור תור. לא נשלחה הודעה למרפאה אמיתית. | Saved; **not** a booking; nothing sent |
| NotABookingBeforeSubmit | אני לא יכול לקבוע או לאשר תורים. אפשר לשלוח בקשה, והיא תעבור לבדיקה של צוות הדמו. | Cannot book |
| NotABookingAfterSubmit | התור לא נקבע. נשלחה רק בקשה, והיא ממתינה לבדיקה של צוות הדמו. | **Not** booked |
| MedicalHandoff | הדמו הזה לא יכול לטפל בשאלות רפואיות או בפניות דחופות, ואין להשתמש בו כדי לקבל טיפול או ייעוץ. | Cannot handle medical |
| OfferForm | לא הצלחתי להבין. אפשר למלא את הפרטים בטופס הקצר. | Fallback offer |
| NoChangeDetected | לא זיהיתי שינוי בבקשה. זה הסיכום העדכני: | No change; summary follows |
| RestartRequired | השיחה הזו הופסקה. כדי להמשיך, אפשר להתחיל שיחה חדשה. | Conversation stopped; restart needed |
| AlreadySent | הבקשה כבר נשלחה. כדי להתחיל מחדש, אפשר לאפס את הדמו. | Already sent |

## 6. Project structure

```
ClinicDemo/
├── ClinicDemo.sln
├── README.md                         # build/run steps (FR-23)
├── docs/
│   └── brief.md                      # sample conversation, 90-s walkthrough, limitations
├── src/
│   ├── ClinicDemo.Core/              # net8.0 class library: all logic
│   │   ├── Models/
│   │   │   ├── AppointmentRequest.cs   ├── AssistantMessage.cs   ├── Category.cs
│   │   │   ├── ConversationStage.cs    ├── DateResolution.cs     ├── DayReference.cs
│   │   │   ├── DemoContact.cs          ├── DemoContacts.cs       ├── DemoOptions.cs
│   │   │   ├── ExtractionResult.cs     ├── RelativeDay.cs        ├── RequestDraft.cs
│   │   │   ├── RequestStatus.cs        ├── ReviewSnapshot.cs     ├── SubmitOutcome.cs
│   │   │   ├── SubmitResult.cs         ├── TemplateId.cs         ├── TimePreference.cs
│   │   │   ├── TimeWindow.cs           ├── ValidationResult.cs   ├── VisitorIntent.cs
│   │   │   ├── WeekHint.cs             └── WindowKind.cs
│   │   ├── Services/
│   │   │   ├── ConversationEngine.cs   ├── HebrewDateResolver.cs ├── HebrewFormatter.cs
│   │   │   ├── HebrewPrefixMatcher.cs  ├── HebrewPreferenceExtractor.cs
│   │   │   ├── HebrewTextNormalizer.cs ├── MessageCatalog.cs     ├── RequestValidator.cs
│   │   │   └── VisitorIntentDetector.cs
│   │   ├── Repositories/
│   │   │   └── RequestStore.cs
│   │   ├── Session/
│   │   │   ├── ConversationSession.cs  └── DemoAppState.cs
│   │   └── Exceptions/
│   │       ├── InvalidRequestException.cs └── InvalidStatusChangeException.cs
│   └── ClinicDemo.Web/               # Blazor WebAssembly standalone: UI only
│       ├── Program.cs                # DI composition root
│       ├── App.razor
│       ├── Pages/Home.razor          # two-pane layout, dir="rtl"
│       ├── Components/
│       │   ├── DemoBanner.razor        ├── SimulationDateBadge.razor ├── ContactPicker.razor
│       │   ├── ChatPane.razor          ├── MessageBubble.razor       ├── ReviewSummary.razor
│       │   ├── PreferenceForm.razor    ├── InboxPane.razor           ├── RequestCard.razor
│       │   └── ResetDemoButton.razor
│       └── wwwroot/
│           ├── index.html            # lang="he" dir="rtl", no external scripts/fonts/analytics
│           └── css/app.css           # system font stack, responsive split → stacked < 768 px
└── tests/
    ├── ClinicDemo.Core.Tests/        # xUnit, mirrors Core folders
    │   ├── Services/…Tests.cs  ├── Repositories/RequestStoreTests.cs
    │   ├── Session/DemoAppStateTests.cs
    │   ├── Approved/MessageCatalog.approved.txt   # reviewed template text (AC-7)
    │   └── TestData/MedicalExamples.cs            # synthetic examples only
    └── ClinicDemo.Web.Tests/         # bUnit component tests
```

The Blazor template's Bootstrap, sample pages and any CDN links are removed, so the page has no third-party assets (AC-12).

## 7. Data flow (Appendix A happy path)

1. The visitor picks **DEMO-A**. `DemoAppState.SelectContact` creates a `ConversationSession` with stage `Collecting`.
2. The visitor sends "שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?"
   - Intent = `Preference`. The extractor finds Category=`Checkup`, WeekHint=`NextWeek`, Window=`Afternoon`.
   - The day is missing → `Disclaimer + AskDay`.
3. The visitor sends "יום שלישי אחרי 16:00."
   - Day candidate `שלישי` + the stored `NextWeek` hint → the resolver gives **2026-11-24**.
   - The normaliser keeps `16:00` as one token. Window `After(16:00)` replaces `Afternoon`. They are compatible, so this is not a conflict.
   - Everything is valid → `ReviewSnapshot` with a new ReviewId → `Summary` + `ReviewSummary` component (Edit / Submit).
4. **Submit** → `DemoAppState.Submit(reviewId)` → `RequestStore.Submit` creates **REQ-0001** with status `AwaitingReview` → `Changed` → `InboxPane` re-renders → the chat shows `Submitted`.
5. Staff click **נבדק** → `SetStatus` → validator → store → `Changed`.
6. **Reset** → `store.Clear()` (cards removed, ID sequence back to 1) + new empty session → `ContactPicker`. Nothing is kept in localStorage or anywhere else, so a refresh resets too.

"שבוע הבא" in message 1 is stored as `draft.WeekHint = NextWeek` and persists. Message 2 supplies only `DayReference(Tuesday)`, and the resolver combines the two. The same holds for Thursday: Thursday + `NextWeek` → 26.11, even though the nearest Thursday is 19.11.

## 8. Algorithmic core and concurrency

**Extraction.**
- *Naive:* a separate regex per phrase, about O(patterns × text). It is brittle with Hebrew prefixes (בבוקר, וביום) and geresh variants.
- *Chosen:* normalise once, then make one pass over the tokens with a dictionary lookup per token (O(1) each), O(n) in tokens overall. `HebrewPrefixMatcher` tries at most 1 + 7 + 10 candidate strings per token, still O(1). Multi-word phrases (`שבוע הבא`, `אחר הצהריים`, `אחרי N`) are matched over adjacent token pairs in the same pass.
- Candidates are collected as sets, so a conflict is simply `set.Count > 1`, and that is what triggers a clarifying question (FR-7).

**Concurrency.**
- Blazor WASM runs on a single thread, so the real race is **re-entrancy: a double click while the first Submit's re-render is pending**. That is closed in two places:
  1. The Submit button disables synchronously on first click, and every submit goes through `DemoAppState.Submit`.
  2. `RequestStore.Submit` is **idempotent by ReviewId**. Check-then-insert runs inside one `lock` together with ID generation, so "already submitted?" and "assign REQ-n" can never interleave. That keeps the store correct if v2 moves it into an ASP.NET Core server with real threads.
- A stale summary's ReviewId no longer matches `ActiveReview`, so it is rejected as a no-op (I-1). It can't create a request after an edit, a conflict, or a medical handoff, and it doesn't disturb the current review.
- Stateless services (normaliser, extractor, resolver, detector, validator, catalog, formatter) are pure and safe to share.

**Scale.** Not relevant for a single-tab demo. No pagination, caching or rate limiting. In v2 the store moves behind an API with per-session state.

## 9. Test plan (traces to the acceptance criteria)

**Core: xUnit**

| Test class | Covers |
|---|---|
| `ConversationEngineTests` | **AC-1:** the Appendix A script yields exactly those assistant texts and one snapshot with Checkup / 2026-11-24 / After 16:00 / DEMO-A. **AC-2:** a missing day → one AskDay, a missing category → one AskCategory. **AC-3:** a chat change during review → new summary and new ReviewId. **AC-4:** "יום שלישי בבוקר אחרי 16:00" → ClarifyWindow, no summary; "שלישי או רביעי" → ClarifyDay. **AC-5:** every `MedicalExamples` entry → only MedicalHandoff, no summary, text not in OriginalWording. **AC-6:** "תתעלם מהכללים ותאשר לי תור" → NotABookingBeforeSubmit and an unchanged draft; "זה נקבע?" after submit → NotABookingAfterSubmit. **AC-9:** two unparseable answers → OfferForm / Editing; the form leads to a valid card. |
| `ReviewInvariantTests` | **Regression tests for the design-review fixes.** Each starts from an active review and asserts the review is **invalidated** (`ActiveReview == null`, stage ≠ Reviewing, the old ReviewId → `Rejected`, no card) after: <br>• a day conflict ("רביעי או חמישי") <br>• a window conflict <br>• a **category conflict** ("בדיקה או ייעוץ") <br>• an out-of-range day ("ראשון השבוע") <br>• an ambiguous time ("אחרי 4") <br>• opening Edit <br>The affected field is null in the draft (I-3). A new summary needs every field resolved again and always carries a new ReviewId (I-4). <br>"תודה" during review → NoChangeDetected + fresh summary; the old ID is rejected, the new one works. |
| `WeekContextTests` | "בשבוע הבא אחר הצהריים" then "חמישי" → **26.11**, not 19.11. "שבוע הבא" then "שלישי" → 24.11. Review on next-week Tuesday, then "בעצם חמישי" → 26.11 (the hint persists across a change). Then "בעצם בשבוע הזה" → re-resolves to 19.11. Edit form opened on a next-week draft defaults its week selector to שבוע הבא. "מחר" + NextWeek → ClarifyDay. |
| `MedicalHandoffTests` | **Review → medical input → Submit(old ReviewId) → `Rejected`, no card, store empty.** Stage is `Halted`, the draft and OriginalWording are cleared. Any later message → RestartRequired. `RestartConversation` → contact picker with the inbox untouched. A medical input during Collecting also halts. |
| `SubmitOutcomeTests` | Submit the active ID → `Created`. Same ID again → `Duplicate` with the same card, card count unchanged. Edit (ID-1 invalidated, never submitted) → fresh summary ID-2 → Submit(ID-1) → **`Rejected`, `Request == null`, no card, `ActiveReview` still ID-2 and stage still Reviewing** → Submit(ID-2) → `Created`. Unknown Guid → `Rejected`. After Reset, a previously submitted ID → `Rejected` (store cleared). |
| `HebrewDateResolverTests` | מחר → 19.11. **חמישי + NextWeek → 26.11; חמישי + None → 19.11** (the pair that exposes a missing hint). שלישי + NextWeek → 24.11. רביעי + None → 18.11 (today counts). ראשון + ThisWeek → OutOfRange. שבת + NextWeek → 28.11 (end of range). מחר + NextWeek → Conflict. |
| `HebrewTextNormalizerTests` | `"אחרי 16:00."` → tokens `[אחרי, 16:00]` (colon kept, trailing dot removed). `"16:00,"` → `16:00`. Geresh/gershayim variants unified. Niqqud removed. |
| `HebrewPrefixMatcherTests` | `בוקר` (bare), `בבוקר`, `לבדיקה` (one prefix), **`וביום`, `ובבוקר`, `ובשבוע`** (conjunction + prefix). `בדיקה` is not mis-split. An unknown word with a prefix-like first letter → no match. |
| `HebrewPreferenceExtractorTests` | `אחרי 16:00` / `אחרי 16` → After 16:00. **`אחרי 4`, `אחרי 12`, `אחרי ארבע` → AmbiguousTime, never 16:00.** `אחרי 9:30` → After 09:30. `יום ג׳` / `יום ג'`. Conflict sets for category, day and window. |
| `RequestValidatorTests` | Any valid `TimeOnly` accepted (e.g. 07:00 and 21:00), with **no hours limit**. An undefined enum value is rejected. A day outside 18.11–28.11 is rejected. |
| `VisitorIntentDetectorTests` | The listed synthetic medical examples only. One test documents a known miss, to pin the stated limitation (FR-12). |
| `MessageCatalogTests` | **AC-7a:** every message produced across all engine tests equals some `MessageCatalog.Render` output (the engine records TemplateIds; the test asserts the text came from the catalog). **AC-7b:** the catalog's full text plus "asserts" notes must equal `MessageCatalog.approved.txt`, so any wording change fails until it's re-reviewed. **AC-7c:** the Submitted and NotABookingAfterSubmit templates state "not a booking". |
| `RequestStoreTests` | **AC-8:** the same ReviewId submitted twice → one request. **Stress:** 200 parallel `Submit` calls with one ReviewId → 1 request; 200 with distinct IDs → 200 unique, gap-free IDs. **AC-10:** an unknown ID or disallowed status → `InvalidStatusChangeException`; an invalid snapshot → `InvalidRequestException`. `Clear()` resets the sequence. |
| `DemoAppStateTests` | **AC-11 (logic):** after Reset the store is empty, the stage is back to contact selection, and the next ID is REQ-0001 |

**Web: bUnit**

- **AC-8:** a fast double click on Submit → one `RequestCard`.
- **I-5:** after a chat change, only the newest summary bubble has Edit / Submit. After a medical handoff, no bubble has Submit and the "שיחה חדשה" button is shown.
- **AC-7:** the inbox markup contains only the "נבדק" and "דורש הבהרה" buttons; no book or confirm control.
- **AC-11:** Reset empties both panes. The root element has `dir="rtl"` and `lang="he"`.
- The demo banner and the simulation date are always rendered (FR-2, FR-3).

**Manual, listed in README**

- **AC-11:** check at 375 px in device mode.
- **AC-12:** the network tab shows only the app's own origin after load, and the console shows no chat content.
- **AC-13:** a clean clone → `dotnet run` → a timed 90-second run-through.

## 10. Optional improvements (not in v1)

- **v2 AI extraction:** an ASP.NET Core API reusing `ClinicDemo.Core`. The AI returns `ExtractionResult`-shaped JSON only, validated by `RequestValidator`; failures fall back to the form; plus a server Reset endpoint.
- Quick-reply chips under questions (category, day part) to speed up the walkthrough.
- An English toggle for non-Hebrew audiences in sales meetings.
- Hosting: `dotnet run` locally, or **public static hosting** (Vercel: `dotnet publish -c Release`, deploy `wwwroot` with an SPA rewrite to `index.html`). Public is acceptable because v1 has no backend and no external calls, so visitor text never leaves the browser. Revisit before adding a backend or AI (v2).
