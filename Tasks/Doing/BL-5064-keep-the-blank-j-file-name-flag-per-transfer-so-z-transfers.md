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
completed:
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

- [ ] The blank-name flag is kept on the running transfer's own state (`RunningTransferState`, or that transfer's output stream), not on the runner.
- [ ] A unit test in `Curl.Console.UnitTests` runs two `-Z -J -O` transfers, only one of them with a blank file name. Only that transfer gets the refusal and exit 3; the other succeeds.
- [ ] A blank-name attempt under `--retry 2 --retry-all-errors` is not retried: no retry warning line, and exit 3. Check this against curl 8.22.0 and pin it with a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: e55419cd e530af4c 223c288a 3d836d5e.
