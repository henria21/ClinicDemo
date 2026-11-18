# ClinicDemo

A Hebrew (RTL) clinic **appointment-request** sales demo: one page with a chat pane and a receptionist inbox. Fictional data only. **It never books or confirms an appointment.**

- What it shows and how to present it: [docs/brief.md](docs/brief.md) (sample conversation, 90-second walkthrough, limitations)
- How it was specified and built: [docs/requirements.md](docs/requirements.md), [docs/design.md](docs/design.md), [docs/tasks.md](docs/tasks.md)

## Prerequisites

- **.NET SDK 9.0.300 or later 9.0.x** ([`global.json`](global.json) pins the 9.0 band). The projects target `net8.0`, which SDK 9 builds directly; no extra workload is needed.
- Internet access for the **first** build only, to restore NuGet packages. The running demo makes no external calls.

Check with:

```
dotnet --list-sdks
```

## Run

From the repository root:

```
dotnet run --project src/ClinicDemo.Web
```

Then open **http://localhost:5209** (the console also prints the URL). Stop it with `Ctrl+C`.

All state lives in browser memory: **איפוס הדמו** or a page refresh resets everything.

## Test

```
dotnet test
```

This runs the Core tests (xUnit: dialogue, Hebrew parsing, dates, validation, the store, the approved message catalog) and the component tests (bUnit: chat, review summary, form, inbox, the Appendix A flow end to end through the page).

The approved assistant text is in [`tests/ClinicDemo.Core.Tests/Approved/MessageCatalog.approved.txt`](tests/ClinicDemo.Core.Tests/Approved/MessageCatalog.approved.txt). Changing any template fails the tests until that file is updated and the Hebrew is reviewed again.

## Manual checks

These can't be automated; run them before presenting.

1. **Phone width (AC-11).** In the browser's device mode at **375 px**, the panes stack, the Hebrew reads right to left, and times and IDs (`16:00`, `REQ-0001`) display correctly inside the text. Nothing scrolls sideways.
2. **No external calls (AC-12).** Open the network tab, reload, and run the walkthrough: every request goes to `localhost:5209` only. The console shows no chat content.
3. **Fresh clone and timing (AC-13).** Clone, follow *Prerequisites* and *Run* with no other steps, then present the [walkthrough](docs/brief.md#90-second-walkthrough) against a timer: **≤ 90 s**.

## Project layout

```
src/ClinicDemo.Core/     all behaviour, no UI: models, Hebrew parsing, dialogue engine, store, message catalog
src/ClinicDemo.Web/      Blazor WebAssembly (standalone), UI only: components, DI wiring, styles
tests/ClinicDemo.Core.Tests/   xUnit
tests/ClinicDemo.Web.Tests/    bUnit
docs/                    requirements, design, tasks, brief
```

## Privacy

The demo stays local while it's being built and shown (`dotnet run`, presented from your laptop). If it ever has to be shared, use **access-controlled hosting** (for example Azure Static Web Apps with authentication, or Cloudflare Access in front). An unlisted URL is not private.
