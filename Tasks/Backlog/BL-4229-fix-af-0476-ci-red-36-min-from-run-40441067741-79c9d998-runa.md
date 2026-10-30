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

## Log

- 2026-10-29: Created.
