---
id: BL-3969
title: Close CGR-170: quality gate, repro evidence and ticked criteria for the -Z setup-failure fix
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3968]
touches: [Curl.Console.UnitTests/Cgr170Tests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3969 — Close CGR-170: quality gate, repro evidence and ticked criteria for the -Z setup-failure fix

## Goal

BL-3699's remaining acceptance work is done: the CGR-170 repros are recorded against real curl and Curl, the quality gate holds, and the fix is documented.

## Context

Split from BL-3699 (over its cost cap), after BL-3968 lands the fix. Repro measurements are in BL-3699's Notes (Deferred); ADR-0866 records the decision.

## Acceptance criteria

- [ ] Each of the three CGR-170 repro commands (with `file://` URLs and the closed port 22099 if the report's server is gone) gives the same exit code, stdout bytes, stderr text, files written and request bytes in real curl and Curl; commands and both results are recorded in Notes.
- [ ] Repro variants that already matched curl still match, and the focused test in `Cgr170Tests.cs` fails without the fix and passes with it (ADR-0482).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` is within every gate, and ADR-0866 and its README row are present and true.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3699.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fee2d392 e7b5cc53 36d6f07e 779790c0.
