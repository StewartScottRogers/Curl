---
id: BL-2215
title: Fix AF-0213: CI red 137.43 minutes from run 38502834786: BL-2177 pushed four methods past the complexity gate without a quality run; the fix waited on a lane held by a stray find / and then on cancelled CI runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-14
completed:
---
# BL-2215 — Fix AF-0213: CI red 137.43 minutes from run 38502834786: BL-2177 pushed four methods past the complexity gate without a quality run; the fix waited on a lane held by a stray find / and then on cancelled CI runs

## Goal

The defect the audit office reported as AF-0213 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0213 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0213-ci-red-137-43-minutes-from-run-38502834786-bl-2177.md`.

Location: `logs/DarkFactory-20261013-133412.log:83`

Location: `logs/DarkFactory-20261013-133412.log:83`

Measure-FactoryProcess.ps1 ciRedSpells: start 2026-10-14T02:57:33Z, end 2026-10-14T05:14:59Z, 137.43 minutes, runId 38502834786 (failure on bc52953f, job Quality gates (windows-latest), step Measure coverage, complexity and CRAP). Cause: lane 3 ran BL-2177 (DarkFactory-20261013-133412-L3.log:58-87, 19:26-19:35 PDT) with build and test steps but no 'quality' step, then pushed at 19:35:18; BL-2181's Done reason reads 'Split the four methods BL-2177 pushed past' the gate. Coordinator filed BL-2181 at 20:05:45 PDT (coordinator log line 84), 8 minutes after red. BL-2181 was claimed only at 20:46:44 (L1.log:488), 41 minutes later: the only lane (lanes 2 -> 1 at 20:26:32) was on BL-2180, whose run ended at 20:23:46 but whose session stayed open until 20:45:11 because a Bash call 'find / -maxdepth 6 -name http_httpsig.c' (BL-2180-20261013-133412-L1.jsonl:57, task bp3krn5d4) ran until it 'was stopped after reaching its background time limit' (jsonl:327). BL-2181 fixed it in 4.4 minutes and pushed at 20:52:05 PDT (03:52Z), but runs 38508458256 through 38512405344 (9 runs, 03:52Z-04:54Z) were all cancelled by later pushes, so CI went green only at 05:14:59Z (run 38513746820 on 636ec5c1), 83 minutes after the fix. The same gap repeated in the window's 28.35-minute spell (run 38519058174 on e8a853b1: BL-2200/2201/2202 pushed with no 'quality' step; BL-2204 fixed it). No master merge on red: 636ec5c was merged at 22:15:23 PDT with run 38513746820 green.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261014-045316\logs -CiRunsJson Z:\repos\Curl.audit\20261014-045316\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 38502834786; Select-String -Path Z:\repos\Curl.audit\20261014-045316\logs\DarkFactory-20261013-133412-L3.log -Pattern 'BL-2177 (quality|push)'; Select-String -Path Z:\repos\Curl.audit\20261014-045316\logs\BL-2180-20261013-133412-L1.jsonl -Pattern 'background time limit' | Measure-Object
```

- Expected: No spell over 30 minutes; every lane push preceded by a quality step; no lane held by a leftover background command
- Actual: Spell 2026-10-14T02:57:33Z to 05:14:59Z, 137.43 minutes, runId 38502834786; BL-2177 shows a push line and no quality line; 1 'background time limit' stop in BL-2180's trace

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
