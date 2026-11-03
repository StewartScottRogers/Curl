---
id: BL-4769
title: Fix AF-0605: Surviving mutant: a header write starting with a bare LF can be taken as one line without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/HeaderLineTeeStreamTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4769 — Fix AF-0605: Surviving mutant: a header write starting with a bare LF can be taken as one line without a test failing

## Goal

The defect the audit office reported as AF-0605 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0605 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0605-surviving-mutant-a-header-write-starting-with-a-ba.md`.

Location: `Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:89`

Location: `Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:89`

lineFeed < 0 -> <= 0 survived (sample and targeted rerun): when the first byte is '\n' the whole buffer is treated as the first line, changing how header lines are split for the header tee.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:89:< -Member LengthOfFirstLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Engine.UnitTests/HeaderLineTeeStreamTests.cs` to touches: the mutant is killed by a test, not a production change, and no task in Doing on origin/work/dark-factory names that file. The production code is right; no change to it.
- Added `Write_` and `WriteAsync_TextStartingWithABareLineFeed_TakesTheLineFeedAsTheFirstLine`: writing `"\nA\r\n"` through one shared stream gives `"\n\nA\r\nA\r\n"`; with `lineFeed <= 0` the whole buffer is one line and gives `"\nA\r\n\nA\r\n"`, so the mutant fails both tests.
- The reproduction script is under `Audit/`, which a lane may not run; the criterion was checked by applying the mutation by hand (`<` to `<=` at HeaderLineTeeStream.cs:89), seeing both new tests fail, and reverting it. The re-audit confirms with the script.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A header write starting with a bare LF is pinned to take the LF as its first line, killing AF-0605's mutant
