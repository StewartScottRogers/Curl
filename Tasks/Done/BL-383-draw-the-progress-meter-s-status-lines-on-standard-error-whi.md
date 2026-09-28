---
id: BL-383
title: Draw the progress meter's status lines on standard error while the transfer runs in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-131]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-383 — Draw the progress meter's status lines on standard error while the transfer runs in Curl.Console

## Goal

`Curl.Console` writes the progress meter's header lines and each status line to standard error as `TransferProgressRecorder` draws it during the transfer, so a terminal sees the line rewritten in place, with standard error's bytes unchanged from BL-131's.

## Context

BL-131 draws the status lines as the handler reports bytes (`Curl.Console/TransferProgressRecorder.cs`) but `CurlCommandRunner.WriteProgressMeterAsync` writes them all after the transfer, so on a terminal the meter appears at the end instead of moving (BL-131 Notes, "Decisions"). curl 8.21.0 writes the header lines at the first draw (`progress_meter`, `headers_out`) and flushes each line. `ITransferProgress` members are synchronous; the write must not block the handler on `.Result`/`.Wait()` (CLAUDE.md), so decide how the recorder reaches standard error (for example a synchronous `Stream.Write` of the already-encoded line) and whether the meter's visibility rules (`ShowsProgressMeter`, and BL-130's "failed before start shows nothing") can be known before the first draw; curl's `-s` / `--no-progress-meter` hide it from the start. Record the decision in an ADR.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` shows a status line reaching standard error before the fake handler returns.
- [x] Every existing progress-meter test in `Curl.Console.UnitTests` (`CurlCommandRunnerProgressMeterTests`, `CurlCommandRunnerStartedTransferProgressMeterTests`, `CurlCommandRunnerLiveProgressMeterTests`) passes unchanged.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; new lines and branches are 100% covered.

## Notes

- Plan (pipeline `feature`, run in-session): `TransferProgressRecorder` takes an optional
  live writer (`Action<string>`); from the first `ReportTransferStarted` it hands the writer
  the lines drawn so far, and after each byte report the lines it drew.
  `CurlCommandRunner.StartTransferProgress` passes `WriteProgressMeterLive` only when
  `ShowsProgressMeter` is true; that method writes the header lines (once per transfer,
  through `TakeProgressMeterHeaderLines`, so `--retry` still gets one header) and the text
  with a synchronous `Stream.Write` + `Flush`. `WriteProgressAsync` now writes
  `TakeUnwrittenStatusLines()` instead of `StatusLines`, so the end draws and newline follow
  and no byte is written twice.
- Decision (ADR-0099, decided by Claude under Stewart's delegation): go live at the first
  "transfer started" report, not at the first draw as curl does, because the first draw is
  made before connect and BL-130 says a transfer that fails before starting shows nothing.
  Every hiding rule (`-s`, `--no-progress-meter`, `-#`, body on a terminal) is known before
  the transfer, so the writer is simply not given then.
- `touches` widened with `Documentation/Planning/Decisions` for ADR-0099 and its README row;
  no task in Doing names it.
- Tests: `CurlCommandRunnerRunningProgressMeterTests` (stderr snapshotted inside the fake
  handler) and three `TransferProgressRecorderTests`; the existing progress-meter test files
  are untouched and pass. Coverage of every changed method (`WriteLive`,
  `TakeUnwrittenStatusLines`, `WriteProgressMeterLive`, `StartTransferProgress`, the three
  reports) is 100% line and branch. Curl.Console.UnitTests: 893 passed.
- Side effect, as in curl: under `-v` the meter and verbose lines now interleave in time order.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The progress meter is written to standard error while the transfer runs, from the first transfer-started report, with BL-131's bytes unchanged
