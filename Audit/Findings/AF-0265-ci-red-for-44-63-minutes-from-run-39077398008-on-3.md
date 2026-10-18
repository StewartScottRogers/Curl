---
id: AF-0265
title: CI red for 44.63 minutes from run 39077398008 on 35c73029: two tests failed on Linux (one also on macOS); fixes were done in 15 min, then 3 cancelled runs
auditor: process
severity: Low
status: closed
reason: Stewart closed it on 2026-10-18 as historical: one past CI red spell, which the factory already guards against ([skip ci] board pushes, the coordinator's CI watch filing the failure), and from ADR-0693 every CI run on work/dark-factory finishes instead of being cancelled.
key: process:logs:39077398008:ci-red
reproduction: none
task: BL-2776
tasks: BL-2776
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: stewart
closed-by: stewart
---
# AF-0265 - CI red for 44.63 minutes from run 39077398008 on 35c73029: two tests failed on Linux (one also on macOS); fixes were done in 15 min, then 3 cancelled runs

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red for 44.63 minutes from run 39077398008 on 35c73029: two tests failed on Linux (one also on macOS); fixes were done in 15 min, then 3 cancelled runs.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-17T23:13:09Z to 23:57:47Z, 44.63 min, run 39077398008. Commit 35c73029 is 'chore(tasks): complete BL-2546 and file BL-2675 and BL-2676'. The failing tests: RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer on Linux and macOS, and Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops on Linux (timing on a busy runner). Lanes test only on Windows. DarkFactory-20261017-082706.log:58 filed BL-2677 at 16:14:57 local, 1.8 min after the failure; line 61 filed BL-2678 at 16:20:41. BL-2677 was DONE at 16:20:22 (L2) and BL-2678 at 16:28:15 (L1). CI then stayed red for about 29 more minutes: runs 39078634089, 39078779623 and 39079145203 were cancelled by newer pushes, and green came only with 39080011902 (created 23:41:50Z, success 23:57:47Z). Shift-end merge f03557f to master at 16:58:19 local came after that green, so no merge happened while CI was red.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-*.log -Pattern '39077398008|BL-267[78] (claim|DONE)'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-17T23:13:09Z 2026-10-17T23:57:47Z 44.63 39077398008 False; BL-2677 DONE 16:20:22, BL-2678 DONE 16:28:15.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed (stewart). Historical: one past CI red spell; no re-audit can change it.
