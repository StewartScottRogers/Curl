---
id: BL-2581
title: Fix AF-0244: Quality gates job red for 103.15 minutes from run 38876049958 on 241c183e: Libcurl CurlMulti.SetOption branch gap, fixed by a separate task, then six cancelled runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-17
completed:
---
# BL-2581 — Fix AF-0244: Quality gates job red for 103.15 minutes from run 38876049958 on 241c183e: Libcurl CurlMulti.SetOption branch gap, fixed by a separate task, then six cancelled runs

## Goal

The defect the audit office reported as AF-0244 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0244 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0244-quality-gates-job-red-for-103-15-minutes-from-run.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-16T09:02:13Z, end 10:45:22Z, 103.15 min, runId 38876049958 (job Quality gates (windows-latest) step Measure coverage, complexity and CRAP) on 241c183e ('chore(tasks): point BL-2436 at ADR-0601'). Coordinator saw it at 02:03:28 local and filed BL-2466 at 02:09:50 (DarkFactory-20261015-200601.log:68-69). BL-2466 claimed 02:25:15, Done 02:43:01 (DarkFactory-20261015-200601-L2.log:588-610); it rewrote Measure-CodeQuality.ps1 and left the actual failing member (Libcurl CurlMulti.SetOption, 87.5% branch) to BL-2467, a Normal-priority task. Its ID collided with BL-2466 and was renumbered at 02:24:12 (L2.log:584); L1 claimed it at 02:58:26 and it was Done at 03:01:55. Run 38880310277 on 0c71f7fb also failed. Six runs between 09:07Z and 10:31Z were cancelled by newer pushes, so the first finished run (38887776973 on 8b514a87) went green only at 10:45:22Z, 43 min after the fix landed. Time to file: 7.6 min; to the real fix: 60 min; cancellation churn: 43 min.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-20261015-200601*.log -Pattern 'BL-2466|BL-2467 (claim|DONE)|38876049958'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-16T09:02:13Z to 10:45:22Z, 103.15 min, runId 38876049958; BL-2466 filed 02:09:50 local, BL-2467 Done 03:01:55 local.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
