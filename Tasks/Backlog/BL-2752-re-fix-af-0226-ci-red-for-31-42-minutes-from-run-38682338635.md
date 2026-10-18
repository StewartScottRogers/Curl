---
id: BL-2752
title: Re-fix AF-0226: CI red for 31.42 minutes from run 38682338635: test203 failed on Linux and macOS after BL-2219's ratchet commit 8d337539
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-18
completed:
---
# BL-2752 — Re-fix AF-0226: CI red for 31.42 minutes from run 38682338635: test203 failed on Linux and macOS after BL-2219's ratchet commit 8d337539

## Goal

The defect the audit office reported as AF-0226 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0226 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0226-ci-red-for-31-42-minutes-from-run-38682338635-test.md`.

Re-fix: the earlier task(s) BL-2279, BL-2563 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-10-18 | 2026-10-18_1045.md | reproduces: yes | The original reproduction still shows the 2026-10-15T04:54:01Z spell (31.42 min, run 38682338635) and 'not merged: CI failure on 8d33753' / 'test203 regression, filed BL-2258'. Its 2026-10-14 spell list also holds an odd entry: 98 min, run 92375573640, on round minutes, with a run id far outside the 38.6e9 range. Re-audited by mechanism over 2026-10-16 onward: a lane commit again turned CI red for over 30 minutes with tests that pass on the Windows-only lanes but fail on Linux and macOS. Run 39077398008 on 35c73029 (BL-2546's finish commit), RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer on Linux and macOS and Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops on Linux, red 2026-10-17T23:13:09Z to 23:57:47Z (44.63 min). Filed as BL-2677 and BL-2678 by the coordinator at 16:14:57 and 16:20:41 local.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

Spell 2026-10-15T04:54:01Z to 05:25:26Z, 31.42 min (over 30), runId 38682338635. Runs 38682338635 and 38682342325 both failed on 8d3375390 (test(conformance): hold 108 more passing upstream cases on the ratchet (BL-2219)). BL-2219 was finished on Windows only (DarkFactory-20261014-172412-L2.log:314,324), and test203 then failed on Linux and macOS. The shift-end merge was correctly withheld (21:54:50 'not merged: CI failure on 8d33753'). BL-2258 was filed at 21:55:18 and claimed at 21:55:42; the fix was done at 22:02:19 (DarkFactory-20261014-215526-L1.log:2,21). The first green run, 38685771852 on 0374c7d5, ended at 05:25:26Z. Cause: a Windows-only lane added an upstream case that depends on the platform.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-14 -LogRoot Z:\repos\Curl.audit\20261015-015611\logs -CiRunsJson Z:\repos\Curl.audit\20261015-015611\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261015-015611\logs\DarkFactory-20261014-172412.log -Pattern 'test203|not merged'
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell 04:54:01Z to 05:25:26Z, 31.42 min, runId 38682338635; 'not merged: CI failure on 8d33753' and 'filed: regression test203 on Linux+macOS'.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
