# ClinicDemo — working notes for Claude

A Hebrew (RTL) clinic **appointment-request** sales demo: one page with a chat pane and a receptionist inbox. Fictional data only. **It never books or confirms an appointment.**

## Read first
- `docs/requirements.md`: approved requirements (FR / NFR / acceptance criteria AC-1..AC-13)
- `docs/design.md`: approved design (Blazor WASM standalone + `ClinicDemo.Core` C# library, review invariants I-1..I-5, test plan)
- `docs/tasks.md`: the ordered task list T1..T14

## How to work
- Implement tasks in **waves**: a task may start once every task it "Depends on" is merged into `main`. Tasks in the same wave may run in parallel, each on its own branch (`task/Tn`) in its own git worktree. Planned waves: T1 → {T2, T3, T5} → {T4, T6, T7} → T8 → T9 → … → T14.
- A task is done only when its "Done when" tests pass with `dotnet test`, **after it is merged into `main`** (full suite green). Tick its checkbox in `tasks.md` on `main` only; task branches don't edit `tasks.md`.
- Worktree subagents stay inside their task's files. If a task needs a shared file changed (`.sln`, `.csproj`, models, `Program.cs`), stop and report instead of editing it.
- The repo lives in OneDrive: pause OneDrive syncing while worktree agents are running.
- Don't change the requirements or the design silently. If a task can't follow them, stop and say why.
- Target `net8.0`. Use xUnit for Core tests and bUnit for component tests. One type per file, named after the type.

## Non-negotiables
- **All visitor-facing assistant text comes from `MessageCatalog`.** Changing any template means updating `tests/ClinicDemo.Core.Tests/Approved/MessageCatalog.approved.txt`, and Henri reviews the Hebrew.
- **Submit rules (I-1):** a ReviewId that was already submitted → `Duplicate` (existing card). The active ReviewId → `Created`. Anything else → `Rejected`, a pure no-op: no card, and the session is untouched.
- **Every change while reviewing invalidates the active review (I-2/I-3).** Medical input → `Halted`, draft discarded, restart required.
- **Times are 24-hour only.** `אחרי 4` is ambiguous and gets a follow-up question; it is never read as 16:00. There are no clinic hours.
- **The week hint persists:** "שבוע הבא" + "חמישי" → 26.11 (simulation date Wed 18.11.2026).
- **No external calls:** no CDN, fonts, analytics or AI in v1, and chat text never goes to logs. No backend in v1.
- The medical keyword list only demonstrates a few synthetic examples. Say so; never claim it detects medical input reliably.
- Public hosting is allowed (static files only, no backend; Vercel project `clinic-demo`). Keep the demo banner visible, never add analytics or external calls, and recheck this rule before adding any backend or AI step.
