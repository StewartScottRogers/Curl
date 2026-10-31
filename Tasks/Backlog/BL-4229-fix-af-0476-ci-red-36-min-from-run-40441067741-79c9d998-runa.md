---
id: BL-4229
title: Fix AF-0476: CI red 36 min from run 40441067741 (79c9d998, RunAsync_OutputInUtf8ConfigFileOnWindows on Linux and macOS)
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
# BL-4229 — Fix AF-0476: CI red 36 min from run 40441067741 (79c9d998, RunAsync_OutputInUtf8ConfigFileOnWindows on Linux and macOS)

## Goal

The cause of the factory behaviour the audit office reported as AF-0476 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0476 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0476-ci-red-36-min-from-run-40441067741-79c9d998-runasy.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

Spell 2026-10-27T03:06:01Z to 03:41:50Z (20:06-20:41 Arizona), 35.82 min. Run 40441067741 failed on 79c9d998 (fix(http): drop HTTP/2 response fields with invalid names, CGR-137) with RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage (DarkFactory-20261026-151632.log:136). The cause was a Windows-only test that was not marked for Windows. BL-3911 was claimed at 20:23:29 (DarkFactory-20261026-151632-L8.log:196) and Done the same evening.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40441067741
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 35.82, runId 40441067741.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix f54935070 (pushed to work/dark-factory 2026-10-30 about 09:10 Arizona, ADR-1158): a ready CI-fix task is claimed before any other work even when its files overlap a task in Doing; the shift's requeued list no longer keeps lanes off it; a CI fix waiting 2 minutes with no free lane gets an extra lane; and its integration goes ahead of ordinary waiters on the integrate lock. "Fix CI build" titles now count as CI fixes. Lanes load the factory at start, so measure from the first shift started after the push. Measured before the fix (since 2026-10-29): spells of 237 min (run 40885502110: BL-4083 requeued behind BL-4082, then skipped as requeued and ranked 6th by next), 139 min (run 40984137987: coordinator silent 17:50-18:45, 41 min waiting for a lane, then a second gate failure waiting 27 min for the integrate lock) and 49 min (run 40832703845). Outside the factory: one CI run takes 11-39 min with queueing, so a spell under 30 min also needs a faster CI run (BL-3512).

## Log

- 2026-10-29: Created.
