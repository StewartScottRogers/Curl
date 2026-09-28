---
id: BL-383
title: Draw the progress meter's status lines on standard error while the transfer runs in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-131]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-383 — Draw the progress meter's status lines on standard error while the transfer runs in Curl.Console

## Goal

`Curl.Console` writes the progress meter's header lines and each status line to standard error as `TransferProgressRecorder` draws it during the transfer, so a terminal sees the line rewritten in place, with standard error's bytes unchanged from BL-131's.

## Context

BL-131 draws the status lines as the handler reports bytes (`Curl.Console/TransferProgressRecorder.cs`) but `CurlCommandRunner.WriteProgressMeterAsync` writes them all after the transfer, so on a terminal the meter appears at the end instead of moving (BL-131 Notes, "Decisions"). curl 8.21.0 writes the header lines at the first draw (`progress_meter`, `headers_out`) and flushes each line. `ITransferProgress` members are synchronous; the write must not block the handler on `.Result`/`.Wait()` (CLAUDE.md), so decide how the recorder reaches standard error (for example a synchronous `Stream.Write` of the already-encoded line) and whether the meter's visibility rules (`ShowsProgressMeter`, and BL-130's "failed before start shows nothing") can be known before the first draw; curl's `-s` / `--no-progress-meter` hide it from the start. Record the decision in an ADR.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` shows a status line reaching standard error before the fake handler returns.
- [ ] Every existing progress-meter test in `Curl.Console.UnitTests` (`CurlCommandRunnerProgressMeterTests`, `CurlCommandRunnerStartedTransferProgressMeterTests`, `CurlCommandRunnerLiveProgressMeterTests`) passes unchanged.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; new lines and branches are 100% covered.

## Notes

## Log

- 2026-09-27: Created.
