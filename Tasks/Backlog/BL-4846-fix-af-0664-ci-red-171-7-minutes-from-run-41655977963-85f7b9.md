---
id: BL-4846
title: Fix AF-0664: CI red 171.7 minutes from run 41655977963 (85f7b9d8), though its failing test was fixed within 4 minutes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-04
completed:
---
# BL-4846 — Fix AF-0664: CI red 171.7 minutes from run 41655977963 (85f7b9d8), though its failing test was fixed within 4 minutes

## Goal

The cause of the factory behaviour the audit office reported as AF-0664 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0664 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0664-ci-red-171-7-minutes-from-run-41655977963-85f7b9d8.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-03T15:01:02Z, end 17:52:45Z, 171.72 min, runId 41655977963, headSha 85f7b9d8 (BL-4729, fix(grpc)). Failing test: CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter on Linux ('Expected strings to be equal'; on a slow runner the progress meter added a one-second refresh line). The CI watch filed BL-4732 at 15:01:54Z (c6ca89a8f, about 1 minute after the failure). Lane 1 claimed it at 15:02:34Z (8ef1d6fe2), and the fix faccb10d4 landed at 15:04:17Z. The other ~165 minutes were three runs whose only non-success jobs were infrastructure, which the tool lists in ciInfrastructureRuns (ADR-1235): 41660275371 (fe004e97) and 41669804192 (429184b1), macOS runner not acquired; 41671248257 (cfbd85f7), the same plus an audit-guard timeout. No CI run was created on the branch from 16:09:22Z to 17:17:00Z. The spell ended only with the next success, 41682710305 (f4a738a9), at 17:52:45Z. So the test-caused red lasted about 4 minutes; the spell's length is the time left with no successful run. Under ADR-1235 those runs neither add red time nor end the spell.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261104-064338\logs -CiRunsJson Z:\repos\Curl.audit\20261104-064338\logs\ci-runs.json -OutFile $env:TEMP\process.json; $j = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; $j.ciRedSpells | Where-Object runId -eq 41655977963; $j.ciInfrastructureRuns | Select-Object -First 3
```

- Expected: No spell over 30 minutes.
- Actual: start 2026-11-03T15:01:02Z end 2026-11-03T17:52:45Z minutes 171.72 runId 41655977963; infrastructure-only runs 41660275371, 41669804192 and 41671248257 inside the spell

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Fix (2026-11-04, interactive): the CI watch now reruns the failed jobs of the newest CI run on the branch whose only bad jobs never got a runner (`gh run rerun --failed`, at most twice per run, `Invoke-CiCapacityRerun` in `DarkFactory/CiWatch.ps1`), and reads the rerun afresh. AF-0664's spell was 165 minutes of such runs waiting for a push that got every runner. -TestCiWatch passes under Windows PowerShell and pwsh (three new BL-4846 cases); `dotnet build` clean and the fast tests pass (115 projects, 0 failed). Criterion 1 waits for a factory shift after the fix lands, so the task goes back to Backlog until then.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-04: Doing -> Backlog. Fix landed; criterion 1 waits for a factory shift's logs after it.
