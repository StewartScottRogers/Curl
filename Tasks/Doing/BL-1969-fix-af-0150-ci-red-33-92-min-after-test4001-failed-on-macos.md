---
id: BL-1969
title: Fix AF-0150: CI red 33.92 min after test4001 failed on macOS from BL-1947's commit (run 38030548658)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-10
completed:
---
# BL-1969 — Fix AF-0150: CI red 33.92 min after test4001 failed on macOS from BL-1947's commit (run 38030548658)

## Goal

The defect the audit office reported as AF-0150 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0150 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0150-ci-red-33-92-min-after-test4001-failed-on-macos-fr.md`.

Location: `logs/DarkFactory-20261009-213949.log`

Location: `logs/DarkFactory-20261009-213949.log`

ciRedSpells: start 2026-10-10T06:25:57Z, end 06:59:52Z, 33.92 min, runId 38030548658, failure on 1c6aa1ac (BL-1947, Curl.Cli quality-gate refactor); test4001 failed on macOS. At 23:27:28 PDT the coordinator logged 'test4001 on macOS only, waiting for the next run'. It filed BL-1949 only at 23:43:05 PDT, after run 38031396189 also failed: 17 minutes after the red. BL-1949 was claimed at 23:43:20, fixed in 336f2af62 at 23:47:49 (macOS server lacks TLS 1.3), and run 38032497110 on 2cf92854 was green at 06:59:52Z. Runs 38031906825 on 3bc50029 also failed in between. No master merge during the spell: merges at 21:39 and 00:44 PDT.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261010-012326\logs\DarkFactory-20261009-213949.log -Pattern 'run 38030548658|filed BL-1949'
```

- Expected: No red spell over 30 minutes
- Actual: spell 06:25:57Z-06:59:52Z, 33.92 min; 23:27:28 'waiting for the next run'; 23:43:05 'filed BL-1949'

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
