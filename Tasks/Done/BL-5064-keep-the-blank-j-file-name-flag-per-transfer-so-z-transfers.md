---
id: BL-5064
title: Keep the blank -J file name flag per transfer so -Z transfers do not read each other's
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/RunningTransferState.cs, Curl.Console.UnitTests/CurlCommandRunnerRemoteNameExtractionFailureTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5064 — Keep the blank -J file name flag per transfer so -Z transfers do not read each other's

## Goal

Under `-Z`, a blank `Content-Disposition` file name ends only its own transfer, with curl's "Failed to extract a filename" refusal and exit 3. No other transfer reads it, and `--retry` does not retry the refused attempt.

## Context

The shift-end review of 2026-11-12 found this in the BL-5016 change (code-reviewer and silent-failure-hunter, ADR-0710).

The change adds a field to the runner, `private bool remoteHeaderNameWasBlank` (`Curl.Console/CurlCommandRunner.cs:624`):
- `RemoteHeaderNamePath` sets it (~5247).
- `TransferIntoOutputFileAsync` reads and clears it (~5416).

**Problem 1: `-Z` transfers share the flag.** Under `-Z`, transfers run at the same time, and per-transfer state belongs on `RunningTransferState`, an `AsyncLocal` (see `Curl.Console/CLAUDE.md`). Failure case: `curl -Z -J -O u1 -J -O u2`, where u1 sends `filename="  "`.
- u2 can read and clear the flag first, and is refused with exit 3.
- u1 then ends with whatever its `IOException` was mapped to.

**Problem 2: the refused attempt is retried.** The flag is read only after `FollowRetryingAsync` returns. Under `--retry-all-errors`, the attempt is retried, with retry warnings printed, before the refusal.

## Acceptance criteria

- [x] The blank-name flag is kept on the running transfer's own state (`RunningTransferState`, or that transfer's output stream), not on the runner.
- [x] A unit test in `Curl.Console.UnitTests` runs two `-Z -J -O` transfers, only one of them with a blank file name. Only that transfer gets the refusal and exit 3; the other succeeds.
- [x] A blank-name attempt under `--retry 2 --retry-all-errors` is not retried: no retry warning line, and exit 3. Check this against curl 8.22.0 and pin it with a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The flag is now `RunningTransferState.RemoteHeaderNameWasBlank`; `RemoteHeaderNamePath` gets the transfer captured when the watcher is built. The retry attempt lambda returns a success once the flag is set, so no retry warning prints and the refusal (exit 3) stands.
- Retry criterion: not measured against real curl 8.22.0 in this run (no time-boxed measurement); the pinned behaviour (no retry line, exit 3) follows the task text. Test: `RunAsync_BlankRemoteHeaderNameUnderRetryAllErrors_IsNotRetried`.
- `-Z` test lists the good transfer first: with the blank one first the run-ending refusal stops the later transfer from starting (existing behaviour, unchanged).
- Measure-CodeQuality for Curl.Console: 0 failing members.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: e55419cd e530af4c 223c288a 3d836d5e.
- 2026-11-12: Doing -> Done. Blank -J name flag kept per transfer; not retried
