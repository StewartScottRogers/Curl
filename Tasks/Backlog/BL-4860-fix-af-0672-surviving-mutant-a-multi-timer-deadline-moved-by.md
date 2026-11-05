---
id: BL-4860
title: Fix AF-0672: Surviving mutant: a multi timer deadline moved by exactly 1 ms can be told to the timer callback again
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4860 — Fix AF-0672: Surviving mutant: a multi timer deadline moved by exactly 1 ms can be told to the timer callback again

## Goal

The defect the audit office reported as AF-0672 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0672 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0672-surviving-mutant-a-multi-timer-deadline-moved-by-e.md`.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:50`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:50`

Mutant '(due - reportedTimerDue).Duration() <= TimeSpan.FromMilliseconds(1)' -> '< TimeSpan.FromMilliseconds(1)' survived (sample -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 413592). The doc comment promises a deadline 'within a millisecond' is not told again. No test pins the 1 ms boundary, so CURLMOPT_TIMERFUNCTION can be called again for a deadline that has not moved.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:50:<= -Member IsTimerUnchanged -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
