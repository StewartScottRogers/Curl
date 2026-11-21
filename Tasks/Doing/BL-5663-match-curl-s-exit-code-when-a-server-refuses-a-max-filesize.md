---
id: BL-5663
title: Match curl's exit code when a server refuses a --max-filesize upload mid-transfer (70 or 55)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineToolTransferOutputs.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5663 — Match curl's exit code when a server refuses a --max-filesize upload mid-transfer (70 or 55)

## Goal

When the server refuses an upload mid-transfer (for example because it is over its size limit), the port's exit code is the one curl 8.22.0 gives, every time, not 70 on some runs and 55 on others.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 6 (check). Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

The exit races between 70 (`RemoteDiskFull`) and 55 (`SendError`) when the server refuses mid-upload (Curl.Api reports `RemoteDiskFull` or `SendError` the same way). The race is likely between reading the server's error reply and the send failing on the closed connection. Measure curl 8.22.0 against the same Surl case, 20 runs, and see whether curl is deterministic; then make the port read the server's reply before reporting the send failure (or whatever curl does), and pin it with a test that forces both orderings.

## Acceptance criteria

- [ ] The Notes record curl 8.22.0's exit codes over 20 runs of the case.
- [ ] The port gives curl's exit code in a test that forces each ordering of the reply and the send failure.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 6 (check).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 2efbce0f 359c8009 3d836d5e.
