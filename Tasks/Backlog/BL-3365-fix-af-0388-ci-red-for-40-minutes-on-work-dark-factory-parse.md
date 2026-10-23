---
id: BL-3365
title: Fix AF-0388: CI red for 40 minutes on work/dark-factory: Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason failed on Linux and macOS
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-23
completed:
---
# BL-3365 — Fix AF-0388: CI red for 40 minutes on work/dark-factory: Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason failed on Linux and macOS

## Goal

The cause of the factory behaviour the audit office reported as AF-0388 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0388 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0388-ci-red-for-40-minutes-on-work-dark-factory-parse-b.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-23T05:36:44Z, end 06:16:53Z, 40.15 minutes, run 39903809116 (failure on 560a15dae), stillRed false. The cause is commit 109584a33 (BL-3183, 21:47 Arizona), which added 'ssl-sessions' to the RefusedForAnotherReason rows of CommandLineBlankArgumentTests. On Linux and macOS (OpenSSL build) curl refuses --ssl-sessions '' as blank: 'Expected collection to not contain the specified element.' The failure showed only at 560a15da because the CI runs in between were cancelled by later pushes. Timeline (Arizona): red at 22:36:44. BL-3203 filed by the CI watch at 22:39:46 (3 min). L5 claimed it at 22:41:47 and pushed the fix f72400608 (OS-pinned tests) at 22:45:46 (9 min after red). Run 39906620565 on cbfaee49, started before the fix, also failed. Green came from run 39907812652 (cd6ec6b2c), which started 22:48:33 and ended 23:16:53. About 31 of the 40 minutes were the length of a full CI run after the fix. Lanes test only on Windows, so a platform-specific test row went untested until CI.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-23T05:36:44Z, end 2026-10-23T06:16:53Z, minutes 40.15, runId 39903809116, stillRed False

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
