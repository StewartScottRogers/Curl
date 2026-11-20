---
id: AF-0904
title: CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:ci-spell-44566263056:ci-red
reproduction: none
task: none
tasks:
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0904 - CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261119-134021.log:13`: CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between.

## Evidence

Location: `logs/DarkFactory-20261119-134021.log:13`

ciRedSpells[0]: start 2026-11-19T17:19:15Z, end 21:11:44Z, 232.48 min, runId 44566263056. Run 44566263056 ended 17:19:15Z (10:19 local); runs 44585647013 (ended 18:42Z) and 44586512674 (ended 19:10Z) also failed inside the spell. The first coordinator ci trace naming any of them is 13:46:12 (DarkFactory-20261119-134021.log), 3h27m, 2h04m and 1h36m after their ends. The previous coordinator log (DarkFactory-20261119-082213.log) ends at 10:19:25 on handover, and the next coordinator started 13:40:24, so nobody watched CI for about 3h20m. The 30-minute rule is crossed. The 14:12:12 trace is the pass-since line. The runner-capacity run 44557476016 is infrastructure and excluded (ADR-1235).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-19 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile process.json; Select-String -Path <log folder>\DarkFactory-20261119-*.log -Pattern '44566263056'
```

- Expected: A ci trace line naming the run (filed, covered or passed since) within 30 minutes of the run ending.
- Actual: First trace 13:46:12 local, run ended 10:19 local; ciRedSpells[0] is 232.48 minutes.

## Re-audits

## Log

- 2026-11-19: filed proposed.
