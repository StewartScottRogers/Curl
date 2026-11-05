---
id: BL-4860
title: Fix AF-0672: Surviving mutant: a multi timer deadline moved by exactly 1 ms can be told to the timer callback again
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/ManagedMultiTransferTests.TimerDeadline.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was already right (`<=` keeps the documented "within a millisecond"); the gap was a test. Added `SocketActionAsync_DeadlineMovedByExactlyOneMillisecond_IsNotToldAgainButOneMovedByTwoIs` in the new `Curl.Engine.UnitTests/ManagedMultiTransferTests.TimerDeadline.cs` (added to `touches`; no task in Doing on work/dark-factory names Curl.Engine.UnitTests). It runs a transfer waiting on its socket with a 1000 ms deadline, moves the deadline to 1001 ms (not told again) and then to 1002 ms (told), on a frozen `GetUtcNow`, because `TimerManualTimeProvider` reads the wall clock for the time of day, so deadlines drift during the settle.
- Reproduction: lanes may not run `Audit/Tools`, so I applied the mutant by hand (`<=` to `<` at ManagedMultiTransfer.Timer.cs:50). The new test failed (`timer 0 | socket 4660 In null | timer 1000 | timer 1001`), so the mutant is killed. I restored the line afterwards. No production change, so no quality run was needed.
- `dotnet build` clean; the fast tests pass (Curl.Engine.UnitTests: 2975 passed, 7 skipped; the solution run: no failures). The test uses no path, OS error or certificate.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A test pins the multi timer's 1 ms deadline boundary, killing AF-0672's mutant
