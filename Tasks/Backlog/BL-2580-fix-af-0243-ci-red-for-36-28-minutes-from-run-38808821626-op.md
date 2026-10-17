---
id: BL-2580
title: Fix AF-0243: CI red for 36.28 minutes from run 38808821626: OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory failed on Linux and macOS after BL-2328's commit 1c068661
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-17
completed:
---
# BL-2580 — Fix AF-0243: CI red for 36.28 minutes from run 38808821626: OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory failed on Linux and macOS after BL-2328's commit 1c068661

## Goal

The defect the audit office reported as AF-0243 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0243 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0243-ci-red-for-36-28-minutes-from-run-38808821626-open.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-15T21:27:53Z, end 22:04:10Z, 36.28 min, runId 38808821626. Commit 1c068661 'fix(core): report ENOTDIR as Not a directory in the -o open warning ... Completes BL-2328', pushed by lane L1 at 14:18:11 local (DarkFactory-20261015-062423-L1.log:570). Lanes test only on Windows; the POSIX-only test failed on Linux+macOS. Coordinator filed BL-2354 at 14:28:17 local, 24 s after the red (DarkFactory-20261015-062423.log:87); the shift ended at 14:40 with 'not merged: CI failure on f4a49d2'; BL-2354 was claimed at 14:41:09 by the next shift's L1 and Done at 14:49:28; run 38810737548 on f4a49d2c also failed; green with run 38813576750 on 82864dc9 at 22:04:10Z. Time to file: 0.4 min; filed to claimed: 13 min (shift changeover); claimed to Done: 8 min; Done to green: 15 min.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-20261015-062423.log -Pattern 'BL-2354|38808821626'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-15T21:27:53Z to 22:04:10Z, 36.28 min, runId 38808821626; BL-2354 filed 14:28:17 local.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
