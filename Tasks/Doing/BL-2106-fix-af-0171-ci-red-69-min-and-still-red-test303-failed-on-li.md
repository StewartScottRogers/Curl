---
id: BL-2106
title: Fix AF-0171: CI red 69+ min and still red: test303 failed on Linux on 0d18262, then two failures naming no test went unfiled
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-12
completed:
---
# BL-2106 — Fix AF-0171: CI red 69+ min and still red: test303 failed on Linux on 0d18262, then two failures naming no test went unfiled

## Goal

The defect the audit office reported as AF-0171 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0171 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0171-ci-red-69-min-and-still-red-test303-failed-on-linu.md`.

Location: `logs/DarkFactory-20261011-152447.log:8`

Location: `logs/DarkFactory-20261011-152447.log:8`

ciRedSpells holds {start 2026-10-11T22:12:33Z, end 23:22:00Z, minutes 69, runId 38178740830, stillRed true}. Run 38178740830 failed on 0d1826298 ('chore(tasks): complete BL-2076', 14:41:16-07:00). Run 38177462122 on the same SHA had passed at 22:00:53Z, so the failure is flaky. test303 had already failed once earlier (run 38154076736 on e67018991, DarkFactory-20261011-141622.log: 'test303 passed since, not filed'). No shift ran when the run failed. The next coordinator filed BL-2093 at 15:26:52 local (22:26:52Z), 14.3 min later ('filed: regression test303 on Linux, run 38178740830'). L1 claimed BL-2093 at 15:28:47 and marked it DONE at 15:34:32 (22:34Z). CI then failed again on b362b849f (run 38182483664, 23:15:09Z) and c811b9eca (run 38183634772, 23:22:00Z). Both times the coordinator wrote 'its log names no failing test or build error; nothing to file', so no task exists and the spell is still open. The shift-end merge to master was withheld twice: 'not merged: CI failure on b362b84' (16:15:22) and 'not merged: CI failure on c811b9e' (16:22:07). No merge happened with CI red, so the severity is under the High bar.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-11 -LogRoot Z:\repos\Curl.audit\20261011-163836\logs -CiRunsJson Z:\repos\Curl.audit\20261011-163836\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 38178740830; Select-String -Path Z:\repos\Curl.audit\20261011-163836\logs\DarkFactory-20261011-1*.log -Pattern 'run 381(78740830|82483664|83634772)|not merged'
```

- Expected: No red spell over 30 minutes; every failing run names a test that a task fixes before the next shift-end merge.
- Actual: Spell from 22:12:33Z, 69 minutes, stillRed true; runs 38182483664 and 38183634772 failed with 'its log names no failing test or build error; nothing to file'; merges withheld on b362b84 and c811b9e.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
