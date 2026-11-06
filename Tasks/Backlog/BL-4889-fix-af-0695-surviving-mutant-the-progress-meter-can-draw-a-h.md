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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
