---
id: AF-0695
title: Surviving mutant: the progress meter can draw a hop's start as done
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:StartHop-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398:false
task: none
tasks:
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0695 - Surviving mutant: the progress meter can draw a hop's start as done

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398`: Surviving mutant: the progress meter can draw a hop's start as done.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398`

StartHop's 'Draw(done: false);' mutated to 'Draw(done: true);' survived. Draw passes done to Calculate, which decides whether a status line is appended (curl's progress_calc req.done). A redirect hop's start could then add or change a progress-meter line on stderr, and no test pins the meter across hops.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398:false -Member StartHop -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.
