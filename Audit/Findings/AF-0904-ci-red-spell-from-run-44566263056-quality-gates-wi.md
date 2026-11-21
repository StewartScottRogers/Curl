---
id: AF-0904
title: CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
key: process:logs:ci-spell-44566263056:ci-red
reproduction: none
task: BL-5592
tasks: BL-5592
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_1740.md
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

- 2026-11-20 | 2026-11-20_0456.md | reproduces: yes | The literal Select-String for run 44566263056 finds nothing. That run ended 17:19Z, before the first copied log line (23:30Z), so it is not evidence of a missing trace. Re-audited by mechanism: run 97190262520 failed at 02:00Z (19:00 MST), the coordinators' ci traces run 17:50 then 20:03 with a gap while no coordinator ran, and the run is never named in any log. The next green run ended 83 minutes later. The window holds shifts that finished 16 tasks. The run id looks synthetic, so the audit run may set the verdict aside.
- 2026-11-20 | 2026-11-20_1740.md | reproduces: no | The copied logs start 2026-11-20 (earliest DarkFactory-20261120-045654.log), and run 44566263056 is absent from them, so the original run could not be traced. Re-audited by mechanism over the 2026-11-20 window, which holds several shifts that finished tasks (46 tasks done). Both red spells were filed in time. Run 44773274042 (ended 16:43Z = 09:43 local) was filed as BL-5609 at 09:38:21. Run 44787815476 (ended 18:15:47Z = 11:15:47 local) was filed as BL-5619 at 11:14:30. No red run went untraced for 30 minutes. ciRedMinutes 50.73.

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
