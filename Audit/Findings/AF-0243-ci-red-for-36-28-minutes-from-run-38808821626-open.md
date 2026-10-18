---
id: AF-0243
title: CI red for 36.28 minutes from run 38808821626: OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory failed on Linux and macOS after BL-2328's commit 1c068661
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0243 - CI red for 36.28 minutes from run 38808821626: OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory failed on Linux and macOS after BL-2328's commit 1c068661

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red for 36.28 minutes from run 38808821626: OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory failed on Linux and macOS after BL-2328's commit 1c068661.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-15T21:27:53Z, end 22:04:10Z, 36.28 min, runId 38808821626. Commit 1c068661 'fix(core): report ENOTDIR as Not a directory in the -o open warning ... Completes BL-2328', pushed by lane L1 at 14:18:11 local (DarkFactory-20261015-062423-L1.log:570). Lanes test only on Windows; the POSIX-only test failed on Linux+macOS. Coordinator filed BL-2354 at 14:28:17 local, 24 s after the red (DarkFactory-20261015-062423.log:87); the shift ended at 14:40 with 'not merged: CI failure on f4a49d2'; BL-2354 was claimed at 14:41:09 by the next shift's L1 and Done at 14:49:28; run 38810737548 on f4a49d2c also failed; green with run 38813576750 on 82864dc9 at 22:04:10Z. Time to file: 0.4 min; filed to claimed: 13 min (shift changeover); claimed to Done: 8 min; Done to green: 15 min.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-20261015-062423.log -Pattern 'BL-2354|38808821626'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-15T21:27:53Z to 22:04:10Z, 36.28 min, runId 38808821626; BL-2354 filed 14:28:17 local.

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: yes | The original reproduction still shows the 36.28-min spell from run 38808821626 and BL-2354 filed at 14:28:17. Re-audited by mechanism over 2026-10-16 onward: a test passing on the Windows-only lanes again failed on Linux and macOS after a lane commit and kept CI red over 30 minutes. Run 39077398008 on 35c73029, RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer on Linux and macOS, red 44.63 min (2026-10-17T23:13:09Z to 23:57:47Z). Fix task BL-2677 (L2, 16:15:23 to 16:20:22 local).

## Log

- 2026-10-16: filed proposed.
