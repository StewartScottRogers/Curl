---
id: AF-0505
title: CI red 76 min from run 41151657472 (dbc468ec) unseen: every coordinator from 15:24 on 10-30 could not list CI runs with gh
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:logs:41151657472:ci-red
reproduction: none
task: BL-4415
tasks: BL-4415
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
---
# AF-0505 - CI red 76 min from run 41151657472 (dbc468ec) unseen: every coordinator from 15:24 on 10-30 could not list CI runs with gh

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261030-193800.log`: CI red 76 min from run 41151657472 (dbc468ec) unseen: every coordinator from 15:24 on 10-30 could not list CI runs with gh.

## Evidence

Location: `logs/DarkFactory-20261030-193800.log`

ciRedSpells: 2026-10-31T01:33:01Z to 02:49:10Z, 76.15 min, runId 41151657472, headSha dbc468ec ('BL-3934 done - the repository is workable on Linux and macOS'), 18:33-19:49 MST 10-30. Every coordinator from 15:24 on logs 'ci cannot list the CI runs on work/dark-factory with gh': DarkFactory-20261030-152226.log:9 (15:24:01), -193026.log (19:31:28), -193800.log (19:38:28), -205631.log (20:56:58), -205849.log (20:59:17), -220408.log (22:05:11) and -20261031-030114.log (03:02:25). No 'ci filed' line appears after BL-4370 at 14:27:09, so this spell got no fix task, and the CI watch (BL-987) stayed blind for about 12 hours of shifts without raising an alarm.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 41151657472; Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-2*.log -Pattern 'cannot list the CI runs'
```

- Expected: No red spell over 30 minutes; the coordinator lists CI runs on every heartbeat.
- Actual: minutes 76.15 runId 41151657472; 8 'cannot list the CI runs on work/dark-factory with gh' lines from 2026-10-30 15:24 on.

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: the coordinators again logged 'cannot list the CI runs on work/dark-factory with gh' at 03:02:25 and 06:06:43. From 06:09:08 on they listed every run. CI was never red in the window (ciRedMinutes 0, no spells), so no red spell went unseen and the CI-red rule was not crossed.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
