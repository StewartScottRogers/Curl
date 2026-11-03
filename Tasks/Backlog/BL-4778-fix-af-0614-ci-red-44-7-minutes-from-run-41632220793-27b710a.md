---
id: BL-4778
title: Fix AF-0614: CI red 44.7 minutes from run 41632220793 (27b710a0): stale ProtoJSON known-failure lines whose fix run was cancelled, then a DoQ regression
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-03
completed:
---
# BL-4778 — Fix AF-0614: CI red 44.7 minutes from run 41632220793 (27b710a0): stale ProtoJSON known-failure lines whose fix run was cancelled, then a DoQ regression

## Goal

The cause of the factory behaviour the audit office reported as AF-0614 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0614 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0614-ci-red-44-7-minutes-from-run-41632220793-27b710a0.md`.

Location: `logs/DarkFactory-20261103-022445.log:41`

Location: `logs/DarkFactory-20261103-022445.log:41`

ciRedSpells: start 2026-11-03T12:19:28Z, end 13:04:10Z, 44.7 minutes, runId 41632220793. That run on 27b710a0 failed RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero, RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray and RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths: known-failure lines kept by a merge that the tree now passes. The fix 1df901115 (05:11 MST) was already on the branch, but its run 41633887707 was cancelled at 12:16:26Z by a newer push. BL-4724/4725/4726 were filed at 05:20:28 MST, 1 minute after the run ended, and closed 05:31-05:34 MST: BL-4724 'Already fixed by 1df901115', BL-4725 'already fixed by 1df901115', BL-4726 'its stale Know~'. Run 41634593982 on 62178b13 (finished 12:31:32Z) then failed Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFi~ and Quic_Query_IsPaddedWithTheEdnsPaddingOption. Filing it failed at 05:34:51 ('could not push the CI failure tasks'), so BL-4727/BL-4728 were filed at 05:36:08 MST and fixed by 05:47:09 MST (bd78278df). Green at 13:04:10Z (run 41639250979 on eae28957), 17 minutes after the last fix. The coordinator cancelled 2 runs that lacked the fixes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-101701\logs -CiRunsJson Z:\repos\Curl.audit\20261103-101701\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41632220793; Select-String -Path Z:\repos\Curl.audit\20261103-101701\logs\DarkFactory-20261103-022445*.log -Pattern 'BL-472[45678] (ci|claim|DONE)|41632220793|41634593982|could not push'
```

- Expected: No red spell over 30 minutes
- Actual: Spell of 44.7 minutes from 41632220793; 3 tasks filed for failures fixed by 1df901115, whose run was cancelled; DoQ regression filed after a failed push

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
