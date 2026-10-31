---
id: AF-0475
title: CI red 121 min from run 40407460127 (f45fc41f, Format_OpenSslUnprintableNames on Linux) while BL-3878 sat unclaimed
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:40407460127:ci-red
reproduction: none
task: BL-4228
tasks: BL-4228
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0475 - CI red 121 min from run 40407460127 (f45fc41f, Format_OpenSslUnprintableNames on Linux) while BL-3878 sat unclaimed

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 121 min from run 40407460127 (f45fc41f, Format_OpenSslUnprintableNames on Linux) while BL-3878 sat unclaimed.

## Evidence

Location: `logs/ci-runs.json`

Spell 2026-10-26T22:15:32Z to 2026-10-27T00:16:54Z (15:15-17:16 Arizona), 121.37 min. Run 40407460127 failed on f45fc41f ('chore(tasks): finish BL-3637') with Format_OpenSslUnprintableNames_PrintsEmptySubjectAndIssuer (DarkFactory-20261026-063535.log:231). The failure was already filed as BL-3878 at 14:30:51 from an earlier run (DarkFactory-20261026-063535.log:195). It was claimed only at 16:24:43, by L2 of the next shift (DarkFactory-20261026-151632-L2.log:31). The task waited about 2 hours, across a shift change, while CI stayed red.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40407460127
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 121.37, runId 40407460127.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
