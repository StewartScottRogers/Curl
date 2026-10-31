---
id: AF-0504
title: CI red 486 min from run 41071219626 (302d7dd3): the quality gates, then a run of five further failures
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:logs:41071219626:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
---
# AF-0504 - CI red 486 min from run 41071219626 (302d7dd3): the quality gates, then a run of five further failures

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261030-060635.log`: CI red 486 min from run 41071219626 (302d7dd3): the quality gates, then a run of five further failures.

## Evidence

Location: `logs/DarkFactory-20261030-060635.log`

ciRedSpells: 2026-10-30T14:42:52Z to 22:48:59Z, 486.12 min, runId 41071219626, headSha 302d7dd3 (07:42-15:48 MST). Six fix tasks were filed in turn. BL-4349 (Quality gates windows-latest): filed 07:52:26, claimed 08:08:28, Done 09:03:11. BL-4359 (Pack Curl.Mcp macos-latest, upload-artifact; its own Done note calls it a 'Transient GitHub artifact-service timeout'): 11:39:14, Done 11:43:30. BL-4360 (flaky quality gates): 12:31:36, Done 12:39:39. BL-4368 (ConnectAsync_WithAnOpenerOnAClosedPort_...): 14:02:42, Done 14:09:17. BL-4369 (the same test again): 14:24:06, Done 15:41:00. BL-4370 (ConnectAsync_ToARealRefusedLoopbackPortOffWindows_...): 14:27:09, Done 15:29:57. Each fix landed and the next failure surfaced. BL-4368's refused-port fix did not hold, so BL-4369 was filed for the same test.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 41071219626
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-30T14:42:52Z end 2026-10-30T22:48:59Z minutes 486.12 runId 41071219626

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: ciRedMinutes 0, no red spells. The last failure (41151657472, dbc468ec) was followed by success 41160265908 at 02:49Z on 10-31, before the window.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
