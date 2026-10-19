---
id: BL-2763
title: Fix AF-0252: UpdateTimer's timer >= 0 can become > 0 with no test failing, so a fired timer's 0 ms re-report to CURLMOPT_TIMERFUNCTION is dropped
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2763 — Fix AF-0252: UpdateTimer's timer >= 0 can become > 0 with no test failing, so a fired timer's 0 ms re-report to CURLMOPT_TIMERFUNCTION is dropped

## Goal

The defect the audit office reported as AF-0252 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0252 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0252-updatetimer-s-timer-0-can-become-0-with-no-test-fa.md`.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:760`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:760`

Engine sample, seed 0: survived. Original `if (timer == reportedTimerMilliseconds && !(timerFired && timer >= 0))`, mutated `timer > 0`. After the timer fires with an unchanged 0 ms timeout, the original calls the application's timer callback again and the mutant returns without calling it. This is a decision visible to libcurl API users that no test pins.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:760:>= -Member UpdateTimer -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary to files, by reading ManagedMultiTransfer.UpdateTimer (line 760) and the multi timer tests, so it can run beside other Engine tasks.
- 2026-10-18: Added `SocketActionAsync_TimerFiresWithAnUnchangedZeroTimeout_ReportsZeroAgain` (a transfer whose 4 ms timeout has expired keeps the timer at 0; two timer actions must each report `timer 0`). The lane may not run the audit office mutation tool (its guard refuses lanes), so the mutant was applied by hand (`timer >= 0` to `timer > 0` at line 760): the new test failed, so the mutant is killed, and the code was put back. No production code changed, so no quality run was needed. The re-audit runs the recorded reproduction itself.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. A fired timer's unchanged 0 ms re-report is pinned by a test; the mutant at ManagedMultiTransfer.cs:760 is killed
