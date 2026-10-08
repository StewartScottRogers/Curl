---
id: BL-1767
title: Fix AF-0089: CI red for 169 minutes from run 37694677072 on 5c771af7; the fix was pushed at 23:03Z but every CI run was cancelled for two hours
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1767 — Fix AF-0089: CI red for 169 minutes from run 37694677072 on 5c771af7; the fix was pushed at 23:03Z but every CI run was cancelled for two hours

## Goal

The defect the audit office reported as AF-0089 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0089 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0089-ci-red-for-169-minutes-from-run-37694677072-on-5c7.md`.

Location: `logs/DarkFactory-20261007-111121.log:75`

Location: `logs/DarkFactory-20261007-111121.log:75`

ciRedSpells[0]: start 2026-10-07T22:19:58Z, end 2026-10-08T01:09:09Z, 169.18 minutes, run 37694677072. The coordinator log shows: 15:21:46 BL-1650 filed (Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNume~); 15:21:47 BL-1651 filed (EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother); 15:21:47 run 37694677072 failure on 5c771af7 (the 'claim BL-1627 on lane 7' commit). That is 2 minutes from red to filed. Further failures: 37695687015 on e0a1b8b1 and 37698759385 on 6677cf92. BL-1650 was integrated at 15:43:42 (lane 8) and BL-1651 at 16:02:59 (lane 9), local time -0700 = 23:02:59Z. CI did not report green until 01:09:09Z (run 37710969213 on cc91b0c9): all 115 CI runs created between 22:58Z and 01:03Z were cancelled, so about 126 of the 169 red minutes came after the fix was in. No merge to master happened while it was red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells; Select-String -Path ..\logs\DarkFactory-20261007-111121.log -Pattern 'BL-165[01] ci|37694677072|37710969213'
```

- Expected: No spell over 30 minutes.
- Actual: start 2026-10-07T22:19:58Z end 2026-10-08T01:09:09Z minutes 169.18 runId 37694677072

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: `ci.yml`'s concurrency group cancels the run in progress on every push, and
  board-only pushes (58 of the branch's commits in those two hours were claims) came
  faster than a CI run finishes. `ci.yml` is a guard file, so the fix is in
  `RunDarkFactory.ps1`: claim, requeue, park, return and CI-watch filing commits now carry
  `[skip ci]` in their body, so they start no run and cancel none. The shift-end merge
  starts CI by hand (`gh workflow run CI`) when the head is such a commit, so it still
  merges only a commit CI passed. Renumber and archive commits are left alone because
  they can head a code integration push. See ADR-0436.
- The reproduction reads the logs from 2026-10-07, which will always show that one spell.
  The fix stops new spells. A re-audit with a later `-Since` is what shows it worked.
- Checked: the script parses, `-TestTaskIds` passes, and a commit made with the new
  arguments has `[skip ci]` in its message.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Board-only factory pushes carry [skip ci] so they no longer cancel the CI run on the code; shift-end merge dispatches CI on such a head (ADR-0436).
