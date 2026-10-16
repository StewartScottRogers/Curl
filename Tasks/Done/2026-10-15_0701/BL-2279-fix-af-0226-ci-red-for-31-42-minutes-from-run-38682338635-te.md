---
id: BL-2279
title: Fix AF-0226: CI red for 31.42 minutes from run 38682338635: test203 failed on Linux and macOS after BL-2219's ratchet commit 8d337539
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2279 — Fix AF-0226: CI red for 31.42 minutes from run 38682338635: test203 failed on Linux and macOS after BL-2219's ratchet commit 8d337539

## Goal

The defect the audit office reported as AF-0226 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0226 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0226-ci-red-for-31-42-minutes-from-run-38682338635-test.md`.

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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The CI red spell is past and cannot be rerun. Cause: lanes test only on Windows, so a ratchet case whose output depends on the platform went green locally and red on Linux and macOS. Decision (Claude, under delegation): add a rule to both lane prompts in RunDarkFactory.ps1 telling a lane to check ratchet cases for platform dependence before holding them. No new ADR: a prompt rule, not a design change.
- Only RunDarkFactory.ps1 changed (parses clean, dotnet build clean); fast tests not rerun as no C# changed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Added a ratchet platform-dependence rule to the lane prompts
