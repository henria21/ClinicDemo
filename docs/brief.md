# Brief — Hebrew Clinic Appointment-Request Demo (v1)

A one-page sales demo for small clinics: a Hebrew (RTL) chat on one side and a receptionist inbox on the other. The fictional clinic is **"מרפאת דמו"**. A visitor, playing a fixed fictional contact, asks for an appointment; the assistant collects only a **general category** and a **preferred day and time window**, shows a summary to confirm, and on Submit creates **one request card** for staff review.

**It never books or confirms an appointment.** The pitch is only: *turn an unstructured inquiry into a complete appointment request.*

## Sample conversation

This is the conversation the demo actually produces, from a fresh page. An automated test (`BriefDialogueTests`) compares the lines between the markers below with the app's output word for word, so they can't drift apart.

> Simulation date: Wednesday 18.11.2026. Contact: איש קשר לדוגמה א׳ (DEMO-A).

<!-- dialogue:start -->
**מבקר לדוגמה:** שלום, אפשר לקבוע תור לבדיקה בשבוע הבא, אחר הצהריים?

**העוזר:** זהו דמו בלבד. אין להזין פרטים אישיים או רפואיים. באיזה יום מועדף לך להגיע?

**מבקר לדוגמה:** יום שלישי אחרי 16:00.

**העוזר:** לסיכום: בקשה לבדיקה, ביום שלישי בשבוע הבא אחרי 16:00, עבור איש קשר לדוגמה א׳. לשלוח את הבקשה לבדיקה של צוות הדמו?

**[שליחת בקשה]**

**העוזר:** בקשת הדמו נשמרה וממתינה לבדיקת הצוות. זה אינו אישור תור. לא נשלחה הודעה למרפאה אמיתית.
<!-- dialogue:end -->

The card that appears in the inbox:

| Field | Value |
|---|---|
| מזהה | REQ-0001 |
| איש קשר | DEMO-A |
| סוג תור | בדיקה |
| העדפת זמן | יום ג׳ 24.11, אחרי 16:00 |
| סטטוס | ממתין לבדיקת צוות |
| הנוסח המקורי | the two visitor messages above |

## 90-second walkthrough

| Time | Do | Say |
|---|---|---|
| 0:00–0:15 | Open the page. Point to the chat, the inbox, the yellow demo banner and the simulation date. | *"This is a simulated request workflow, not a live booking system. It uses fictional contacts, and its medical-question handling covers only a few sample phrases."* |
| 0:15–0:45 | Pick **איש קשר לדוגמה א׳**. Send the first visitor message, show the day question, send the second, show the summary with **עריכה** and **שליחת בקשה**. | *"It asks only for what's missing — here, the day — and reads the visitor's own words back before anything is sent."* |
| 0:45–1:05 | Click **שליחת בקשה**. Point to the "זה אינו אישור תור" message and the new card: structured fields beside the original wording. | *"Staff get a complete request, with the visitor's wording next to it."* |
| 1:05–1:20 | Click **נבדק** on the card. | *"Staff mark it reviewed. No slot was booked anywhere."* |
| 1:20–1:30 | Ask the question. | *"How do requests reach your team today, and which information do you need before calling someone back?"* |

To show a second request next to the first, click **שיחה חדשה** under the chat after submitting: it starts a new chat and keeps the inbox. Before the next run, click **איפוס הדמו** (or refresh the page): it clears the cards and the chat.

If there's interest, scope a separate pilot around their real process: channels, consent and privacy, data handling, calendar rules, staff ownership. The demo itself only proves that the workflow runs.

## Stated limitations

Say these out loud when they're relevant; they are also why the demo is safe to show.

- **Medical handling is keyword-based and demonstrates a few synthetic examples only.** A message like "יש לי כאב חזק, מה לקחת?" stops the conversation with a fixed statement, but the list is small and **will not catch every Hebrew phrasing** — a known miss is pinned by a test. The demo-only banner, not detection, is the safeguard.
- **Fictional data only.** Three fixed demo contacts (DEMO-A/B/C); there are no name, phone or email fields. Don't type real personal or medical details.
- **No availability and no booking.** The demo never checks a calendar, has no clinic hours, and never confirms a slot. A card is a request waiting for staff, nothing more.
- **Rule-based Hebrew, 24-hour times.** The assistant understands the scripted phrasings and common variants. Times must be 24-hour: "אחרי 4" gets a follow-up question and is never read as 16:00. An exact time ("ב-20:00") can't be requested; the assistant says so and offers "אחרי 20:00" or a part of the day. Anything it can't read twice in a row opens a short form instead.
- **Browser only.** No backend, no AI, no analytics, no external calls; chat text is kept in browser memory and never logged. It can run locally or from the public static demo link; either way nothing a visitor types leaves their browser.
