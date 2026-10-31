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

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
