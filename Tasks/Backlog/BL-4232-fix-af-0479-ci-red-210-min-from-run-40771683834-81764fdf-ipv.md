---
id: BL-4232
title: Fix AF-0479: CI red 210 min from run 40771683834 (81764fdf): IPv6/IPv4-literal tests plus two tests CiWatch named '-4' and '-6'
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed:
---
# BL-4232 — Fix AF-0479: CI red 210 min from run 40771683834 (81764fdf): IPv6/IPv4-literal tests plus two tests CiWatch named '-4' and '-6'

## Goal

The cause of the factory behaviour the audit office reported as AF-0479 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0479 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0479-ci-red-210-min-from-run-40771683834-81764fdf-ipv6.md`.

Location: `logs/DarkFactory-20261028-145549.log:28`

Location: `logs/DarkFactory-20261028-145549.log:28`

Spell 2026-10-28T21:43:59Z to 2026-10-29T01:14:26Z (14:43-18:14 Arizona), 210.45 min, on 81764fdf ('chore(tasks): finish BL-3593'). At 15:21:45 CiWatch filed BL-3991 'regression -4 on Linux+macOS', BL-3992 'regression -6', BL-3993 ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy and BL-3994 OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral (DarkFactory-20261028-145549.log:28-31). Two task titles are fragments ('-4', '-6') because the failing-test parse misread the output. The spell began during the shift restart 14:48-15:07, whose coordinator 20261028-144818 started no lane.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40771683834; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-20261028-145549.log -Pattern 'filed: regression -[0-9]'
```

- Expected: No red spell over 30 minutes; CI tasks named for real tests.
- Actual: minutes 210.45, runId 40771683834; BL-3991 and BL-3992 filed as '-4' and '-6'.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. ciRedSpells since fix f54935070 (ADR-1158, 10-30 09:10): two, 44 min (run 41128846186, 10-30 15:04-15:48) and 76 min (run 41151657472, test670, 10-30 18:33-19:49); both fall inside the spell when every coordinator was blind to CI (the timed gh wrapper space-joined --json a,b, from BL-4353 at 10-30 09:06), so no task was filed. That cause was fixed by 8c14126f5 (BL-4384, running from the 06:08 shift on 10-31); since then: no red spell over 30 min, the CI watch listed the runs on every heartbeat, and the shift-end merge wait could see CI. The finding's incident predates both fixes.

- Fix f54935070 (pushed to work/dark-factory 2026-10-30 about 09:10 Arizona, ADR-1158): a ready CI-fix task is claimed before any other work even when its files overlap a task in Doing; the shift's requeued list no longer keeps lanes off it; a CI fix waiting 2 minutes with no free lane gets an extra lane; and its integration goes ahead of ordinary waiters on the integrate lock. "Fix CI build" titles now count as CI fixes. Lanes load the factory at start, so measure from the first shift started after the push. Measured before the fix (since 2026-10-29): spells of 237 min (run 40885502110: BL-4083 requeued behind BL-4082, then skipped as requeued and ranked 6th by next), 139 min (run 40984137987: coordinator silent 17:50-18:45, 41 min waiting for a lane, then a second gate failure waiting 27 min for the integrate lock) and 49 min (run 40832703845). Outside the factory: one CI run takes 11-39 min with queueing, so a spell under 30 min also needs a faster CI run (BL-3512).

## Log

- 2026-10-29: Created.
