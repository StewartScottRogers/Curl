---
id: AF-0330
title: CI red 48.4 min on work/dark-factory from run 39691487971: Windows quality gate (coverage, complexity, CRAP) failed after BL-2856's push
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:39691487971:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0330 - CI red 48.4 min on work/dark-factory from run 39691487971: Windows quality gate (coverage, complexity, CRAP) failed after BL-2856's push

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261021-150011.log`: CI red 48.4 min on work/dark-factory from run 39691487971: Windows quality gate (coverage, complexity, CRAP) failed after BL-2856's push.

## Evidence

Location: `logs/DarkFactory-20261021-150011.log`

ciRedSpells: start 2026-10-21T23:21:56Z, end 2026-10-22T00:10:17Z, 48.35 min, runId 39691487971. First failing commit 01deb2980 ('chore(tasks): complete BL-2856 and file BL-2996...'), job 'Quality gates (windows-latest) step Measure coverage, complexity and CRAP'. The coordinator saw it at 16:23:12 MST and filed BL-2998 at 16:29:31. L1 claimed it at 16:47:29 and pushed DONE at 16:56:44 ('WriteBytesAsync split'). Run 39696631298 then succeeded. About 18 min passed between filing and claim. The lane integrate step logs 'quality ok' but did not catch a member outside the gate that the CI quality job measures.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39691487971
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-21T23:21:56Z, end 2026-10-22T00:10:17Z, minutes 48.35

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
