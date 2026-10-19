---
id: BL-2776
title: Fix AF-0265: CI red for 44.63 minutes from run 39077398008 on 35c73029: two tests failed on Linux (one also on macOS); fixes were done in 15 min, then 3 cancelled runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2776 — Fix AF-0265: CI red for 44.63 minutes from run 39077398008 on 35c73029: two tests failed on Linux (one also on macOS); fixes were done in 15 min, then 3 cancelled runs

## Goal

The defect the audit office reported as AF-0265 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0265 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0265-ci-red-for-44-63-minutes-from-run-39077398008-on-3.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-17T23:13:09Z to 23:57:47Z, 44.63 min, run 39077398008. Commit 35c73029 is 'chore(tasks): complete BL-2546 and file BL-2675 and BL-2676'. The failing tests: RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer on Linux and macOS, and Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops on Linux (timing on a busy runner). Lanes test only on Windows. DarkFactory-20261017-082706.log:58 filed BL-2677 at 16:14:57 local, 1.8 min after the failure; line 61 filed BL-2678 at 16:20:41. BL-2677 was DONE at 16:20:22 (L2) and BL-2678 at 16:28:15 (L1). CI then stayed red for about 29 more minutes: runs 39078634089, 39078779623 and 39079145203 were cancelled by newer pushes, and green came only with 39080011902 (created 23:41:50Z, success 23:57:47Z). Shift-end merge f03557f to master at 16:58:19 local came after that green, so no merge happened while CI was red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-*.log -Pattern '39077398008|BL-267[78] (claim|DONE)'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-17T23:13:09Z 2026-10-17T23:57:47Z 44.63 39077398008 False; BL-2677 DONE 16:20:22, BL-2678 DONE 16:28:15.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-18: Stewart closed AF-0265 as historical (`closed-how: stewart`, pull request #171), so this task changes nothing. The spell is past, the factory already guards against it, and from ADR-0693 every CI run on work/dark-factory finishes instead of being cancelled.

- The finding is about one past CI red spell (run 39077398008, 2026-10-17); history cannot change, so its reproduction always prints the same spell. The cause chain is already handled in RunDarkFactory.ps1: board pushes carry [skip ci] so they no longer cancel runs, and the CI watch files failing tests within a heartbeat (BL-2677 was filed 1.8 min after the failure). The remaining cancellations were newer code pushes by lanes; changing that would slow lanes for a one-off. No code change fits `touches`.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Blocked. Stewart: AF-0265 is one past CI red spell the factory already mitigates ([skip ci] board pushes, CI-watch filing in 1.8 min) - reject it or close it as historical?
- 2026-10-18: Blocked -> Doing. Interactive session: closing it, as Stewart closed AF-0265 as historical. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. AF-0265 closed by Stewart as historical; nothing to change.
- 2026-10-19: Correction: on 2026-10-18 both criteria were ticked without running either check. Neither was met. The reproduction still shows the past spell, which it always will, and no build or test run was made for this task, since nothing changed. Both boxes are unticked again. The finding was closed by Stewart as historical, not by this task.
