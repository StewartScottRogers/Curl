---
id: AF-0162
title: CI red 33.92 min (run 38030548658, test4001 on macOS); the same spell as AF-0150, counted again because -Since is applied in UTC
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-11_1638.md: the reproduction no longer reproduces.
key: process:logs:test4001:ci-red
reproduction: none
task: BL-2055
tasks: BL-2055
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed: 2026-10-11
closed-how: reliable-reaudit
closed-by: 2026-10-11_1638.md
---
# AF-0162 - CI red 33.92 min (run 38030548658, test4001 on macOS); the same spell as AF-0150, counted again because -Since is applied in UTC

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 33.92 min (run 38030548658, test4001 on macOS); the same spell as AF-0150, counted again because -Since is applied in UTC.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells[0]: start 2026-10-10T06:25:57Z, end 06:59:52Z, 33.92 min, runId 38030548658, commit 1c6aa1ac. The runs that followed, 38031396189 and 38031906825, also failed, and 38032497110 succeeded on 2cf92854. In local time (UTC-7) this is 23:25 to 23:59 on 2026-10-09, inside the previous audit's window. The previous audit's coordinator log filed BL-1949 at 23:43:05, 17 min after the red. This is the incident AF-0150 records. It enters this window's ciRedMinutes (92.12) because the tool compares UTC times with a -Since date, so a spell from the previous local evening is counted twice across audits.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 38030548658
```

- Expected: No spell over 30 minutes.
- Actual: start 2026-10-10T06:25:57Z end 2026-10-10T06:59:52Z minutes 33.92 runId 38030548658 stillRed False

## Re-audits

- 2026-10-11 | 2026-10-11_1638.md | reproduces: no | Ran the tool with -Since 2026-10-11. The tool now compares CI run start with -Since converted to UTC (Measure-FactoryProcess.ps1:66-67, 156). ciRedSpells start at 16:12:04Z, 18:05:00Z and 22:12:33Z, all inside the window. Run 38030548658 is not among them, and no spell from an earlier audit's window is counted again.

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
- 2026-10-11: accepted -> closed. Re-audit 2026-10-11_1638.md: the reproduction no longer reproduces.
