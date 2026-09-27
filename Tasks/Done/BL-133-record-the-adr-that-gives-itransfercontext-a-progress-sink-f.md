---
id: BL-133
title: Record the ADR that gives ITransferContext a progress sink for the progress meter
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-102]
touches: [Documentation/Planning/Decisions, Tasks/Backlog/BL-134-add-the-transfer-progress-sink-to-itransfercontext-in-curl-p.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-133 — Record the ADR that gives ITransferContext a progress sink for the progress meter

## Goal

An Accepted ADR under `Documentation/Planning/Decisions/` decides how a protocol handler tells `Curl.Console` that a transfer has started and how many bytes it has moved, so the progress meter can show live counters and appear after a failure that happened past connect/open.

## Context

BL-102 made `Curl.Console` write the opening of curl 8.21.0's progress meter to stderr after each successful transfer (`Curl.Console/ProgressMeterLines.cs`, `CurlCommandRunner.WriteProgressMeterAsync` / `ShowsProgressMeter`). It cannot do more, because a handler reports nothing until it returns its `TransferResult`. Two gaps need information from inside the transfer (measured on 2026-09-26 with curl 8.21.0, x86_64-w64-mingw32, against a local `python -m http.server`, stderr redirected to a file):

1. **Live counters.** For a network transfer curl rewrites the status line in place, `\r`-separated with no newline between, ending in one newline. A 10-byte file gave:
   `\r  0      0   0      0   0      0      0      0                              0\r100     10 100     10   0      0    244      0                              0\r100     10 100     10   0      0    241      0                              0\r100     10 100     10   0      0    238      0                              0\r\n`
   A 200000-byte file gave lines such as `\r100 195.3k 100 195.3k   0      0 46.52M      0                              0`. The counters need bytes received/sent and the expected total (when known).
2. **Meter after a failure.** curl prints the meter when the transfer got past connect/open and then failed (`curl -C 5 http://...` against a server without range support prints the meter, then `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`), but not when it failed before (a `file://` of a missing file prints only `curl: (37) Could not open file ...`). The console needs a "transfer started" signal.

`ITransferContext` is the only channel from `Curl.Console` into a handler (ADR-0003, ADR-0006, ADR-0008). ADR-0003, ADR-0006 and ADR-0008 are Accepted and immutable (`Documentation/Planning/Decisions/README.md`), so this is a new ADR alongside them, numbered with the next free ADR number (ADR-0009 at filing time; take whatever is next when you run). Follow the structure of `ADR-0008-transfer-context-carries-connect-timeout-and-max-time.md`.

The shape to decide, as a starting proposal the ADR may refine but must settle by name: a new interface in `Curl.Protocol.Abstractions.UnitLibrary` (for example `ITransferProgress` with `ReportTransferStarted()`, `ReportDownloaded(long bytesSoFar, long? expectedTotal)` and `ReportUploaded(long bytesSoFar, long? expectedTotal)`), an `ITransferContext` member exposing it, a do-nothing implementation that `TransferContext` defaults to so every existing handler and test keeps compiling unchanged, and the rule that the sink never reads the clock itself: timing belongs to the consumer in `Curl.Console`, which uses the injected `TimeProvider` (`ITransferContext.TimeProvider`). The ADR must also say that reporting is synchronous and cheap (no `async` on the sink), since handlers call it in their copy loops, and that no handler is obliged to report bytes (`file://` must not, because curl's `file://` status line stays all zeros, per BL-102's Notes).

This is a docs-only task: no `.cs` file changes. The contract is BL-134.

## Acceptance criteria

- [x] A new ADR file exists under `Documentation/Planning/Decisions/` with Status `Accepted`, naming the interface, each of its members with its parameters, the `ITransferContext` member, and the do-nothing default `TransferContext` uses.
- [x] The ADR's Context quotes the two curl 8.21.0 measurements above (the 10-byte status-line sequence and the exit 33 after the meter) with the date and curl version.
- [x] The ADR states that the sink does not read time, that time is measured by `Curl.Console` through the injected `TimeProvider`, and that `file://` reports "started" but no byte counts.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR if that file keeps an index.

## Notes

- Written in the session rather than through `align-and-document`: a single new ADR plus an index row, no names or code to align.
- ADR number: ADR-0045, the next free number on 2026-09-26 (ADR-0009 was taken since filing, and ADR-0044 by BL-178 on another lane during the rebase).
- Decisions (ADR-0045, decided by Claude under Stewart's delegation): `ITransferProgress` with `ReportTransferStarted()`, `ReportDownloaded(long bytesSoFar, long? expectedTotal)`, `ReportUploaded(long bytesSoFar, long? expectedTotal)`; running totals, not deltas; `ITransferContext.Progress` with no default interface implementation, so a direct implementer must choose; `TransferContext.Progress` defaults to `NoTransferProgress.Instance`.
- Found: `RedirectFollower.NextHop` copies every context member into a new `TransferContext` by hand, and the default means the compiler will not catch a missing `Progress`. Filed BL-306 (touches `Curl.Core.UnitLibrary`, `Curl.Core.UnitTests`, depends on BL-134) rather than widening BL-134.
- Added BL-134's task file to `touches` to point its Context at ADR-0045 and correct a stale fact (`FakeTransferContext.cs` no longer exists). No task in Doing names it.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ADR-0045 decides ITransferProgress on ITransferContext.Progress with NoTransferProgress as the default; BL-306 filed for redirect hops
