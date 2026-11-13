---
id: AF-0695
title: Surviving mutant: the progress meter can draw a hop's start as done
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:StartHop-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:398:false
task: BL-4889
tasks: BL-4889
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-12
closed-how: mechanical
closed-by: 2026-11-12_2105.md
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

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the -Site command: resolved to line 397 in StartHop, 'bytesReported = false;' -> 'true;', which was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
