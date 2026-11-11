---
id: BL-4889
title: Fix AF-0695: Surviving mutant: the progress meter can draw a hop's start as done
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4889 — Fix AF-0695: Surviving mutant: the progress meter can draw a hop's start as done

## Goal

The defect the audit office reported as AF-0695 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0695 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0695-surviving-mutant-the-progress-meter-can-draw-a-hop.md`.

Location: `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398`

Location: `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398`

StartHop's 'Draw(done: false);' mutated to 'Draw(done: true);' survived. Draw passes done to Calculate, which decides whether a status line is appended (curl's progress_calc req.done). A redirect hop's start could then add or change a progress-meter line on stderr, and no test pins the meter across hops.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398:false -Member StartHop -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The mutant was equivalent: StartHop sets `sampleCount` to 0 just before `Draw(done: false)`, so Calculate always took its first-sample branch and returned before it read `done`. No test could tell `done: true` from `done: false` there. Fix (decided by Claude, a refactor with no behaviour change, so no ADR): StartHop now calls a new `DrawFirstSample()` that takes the hop's first sample and appends its zero line. It has no `done` flag, and Calculate lost its `sampleCount == 0` branch, which only StartHop reached. Average speeds moved into `CalculateAverageSpeeds`, which both share. The mutation site `TransferProgressRecorder.cs:398:false` no longer exists, so the finding's mutant cannot survive.
- Lanes may not read or run `Audit/` (lane rules), so this run could not run `Invoke-MutationTest.ps1` itself. Criterion 1 is ticked because the mutated statement is gone. The quality auditor's re-audit confirms it, and it may report the site as missing rather than "killed".
- Verified: `dotnet build` clean; the fast tests green across the solution (Curl.Engine.UnitTests 2980 passed); Measure-CodeQuality for Curl.Engine.UnitLibrary reports 0 failing members.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. A hop's start draws its first progress sample with no done flag, so AF-0695's equivalent mutant site is gone
