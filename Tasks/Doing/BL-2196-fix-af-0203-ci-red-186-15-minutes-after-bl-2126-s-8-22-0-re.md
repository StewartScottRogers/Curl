---
id: BL-2196
title: Fix AF-0203: CI red 186.15 minutes after BL-2126's 8.22.0 re-vendor broke test798, test1136 and test1462; fixes waited 2h15m for a running audit
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-14
completed:
---
# BL-2196 — Fix AF-0203: CI red 186.15 minutes after BL-2126's 8.22.0 re-vendor broke test798, test1136 and test1462; fixes waited 2h15m for a running audit

## Goal

The defect the audit office reported as AF-0203 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0203 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0203-ci-red-186-15-minutes-after-bl-2126-s-8-22-0-re-ve.md`.

Location: `logs/DarkFactory-20261013-092151.log:20`

Location: `logs/DarkFactory-20261013-092151.log:20`

ciRedSpells[1] runs from 2026-10-13T16:51:50Z to 19:57:59Z (09:51 to 12:57 local), 186.15 minutes, with runId 38431484503: a failure on 48919fca, the merge of BL-2126's commit 0d4389385 'test(conformance): re-vendor upstream test data at curl-8_22_0'. Before pushing, lane 1 logged 'BL-2126 verify build and fast tests green on the shared branch' at 09:33:12, yet CI failed test798, test1136 and test1462 on Linux, macOS and Windows. The coordinator filed BL-2134, BL-2135 and BL-2136 at 09:52:56, one minute after the failure, and refused the master merge ('not merged: CI failure on 48919fc'). The next shift then logged 'An audit is running; shifts start between audits' every 5 minutes from 09:53:06 to 12:03:10 (DarkFactory-20261013-095305.log). BL-2134 was claimed at 12:08:26. Its run then failed test1462 twice on the combined tree and needed a repair run (12:15:00 to 12:19:20). BL-2136 pushed at 12:28:24, and run 38457440621 went green on 96f2d88d at 19:57:59Z. 135 of the 186 red minutes were the wait for the audit to finish. No merge to master happened while CI was red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261013-182203\logs -CiRunsJson Z:\repos\Curl.audit\20261013-182203\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells; Select-String -Path Z:\repos\Curl.audit\20261013-182203\logs\DarkFactory-20261013-092151.log,Z:\repos\Curl.audit\20261013-182203\logs\DarkFactory-20261013-095305.log -Pattern '38431484503|An audit is running' | Measure-Object
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell from 16:51:50Z to 19:57:59Z, 186.15 minutes, runId 38431484503. The coordinator logs show 27 'An audit is running' waits between filing (09:52:56) and the first claim (12:08:26).

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
