---
id: AF-0672
title: Surviving mutant: a multi timer deadline moved by exactly 1 ms can be told to the timer callback again
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:IsTimerUnchanged-le:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:50:<=
task: BL-4860
tasks: BL-4860
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed: 2026-11-05
closed-how: mechanical
closed-by: 2026-11-05_0819.md
---
# AF-0672 - Surviving mutant: a multi timer deadline moved by exactly 1 ms can be told to the timer callback again

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:50`: Surviving mutant: a multi timer deadline moved by exactly 1 ms can be told to the timer callback again. Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:50`

Mutant '(due - reportedTimerDue).Duration() <= TimeSpan.FromMilliseconds(1)' -> '< TimeSpan.FromMilliseconds(1)' survived (sample -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 413592). The doc comment promises a deadline 'within a millisecond' is not told again. No test pins the 1 ms boundary, so CURLMOPT_TIMERFUNCTION can be called again for a deadline that has not moved.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs:50:<= -Member IsTimerUnchanged -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-05 | 2026-11-05_0819.md | reproduces: no | Ran the -Site command: '(due - reportedTimerDue).Duration() <= TimeSpan.FromMilliseconds(1)' -> '<' at line 50, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
