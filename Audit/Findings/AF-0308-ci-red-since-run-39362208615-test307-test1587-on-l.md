---
id: AF-0308
title: CI red since run 39362208615 (test307, test1587 on Linux and macOS) and still red at log copy; fix tasks sat 43 min unclaimed because the weekly window was used up
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:39362208615:ci-red
reproduction: none
task: BL-2946
tasks: BL-2946
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0308 - CI red since run 39362208615 (test307, test1587 on Linux and macOS) and still red at log copy; fix tasks sat 43 min unclaimed because the weekly window was used up

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red since run 39362208615 (test307, test1587 on Linux and macOS) and still red at log copy; fix tasks sat 43 min unclaimed because the weekly window was used up.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells[2]: start 2026-10-20T03:29:12Z, 49 min to the last failure at 04:18:07Z, runId 39362208615, head c8ae701d, stillRed true. Failing tests: upstream cases test307 and test1587 on Linux and macOS. Timeline (MST, 2026-10-19): red at 20:29:12. BL-2873 and BL-2874 filed at 20:31:43-44 (DarkFactory-20261019-192417.log:35-36). Shift 192417 was holding its 9 lanes ('weekly tokens 97% used'), claimed nothing, and ended at 20:49:10. Shift 211203 refused to start: 'BL-2827 is in Doing and held by no lane', ALARM. Shift 211325 claimed BL-2873 at 21:14:12, 42.5 min after filing, and finished it at 21:24:02. Shift 212549 claimed BL-2874 at 21:26:10 and finished it at 21:32:17. Both fixes took the cases off the ratchet. Runs on 53b1837c and 54e24ef3 failed too. The shift-end merge was refused: 'not merged: CI failure on 54e24ef' (192417.log:60). At copy, the runs on 0a062cf and 1b3c70b were still pending.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39362208615
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-20T03:29:12Z end 2026-10-20T04:18:07Z minutes 49 runId 39362208615 stillRed True

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | still reported
- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction gave 49 min, stillRed=true at that log copy. In this window the same spell 39362208615 runs to 2034.67 min, ending 2026-10-21T13:23:52Z. About 32 h of that was with no shift running: DarkFactory-20261019-215417.log 21:54:28 'weekly tokens 97% used; this shift starts when they reset, Wednesday 21 Oct 06:00'. There were no CI runs between 2026-10-20T06:56Z and 2026-10-21T13:09Z. The cause crossed the rule inside this window. Since BL-2946 (ADR-0744, Done 2026-10-21 14:15), the later spells 39691487971 and 39718635967 were not held by a usage stop.

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
