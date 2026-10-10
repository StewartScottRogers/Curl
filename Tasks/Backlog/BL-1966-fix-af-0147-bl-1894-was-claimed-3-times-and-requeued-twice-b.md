---
id: BL-1966
title: Fix AF-0147: BL-1894 was claimed 3 times and requeued twice before one run finished it
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-10
completed:
---
# BL-1966 — Fix AF-0147: BL-1894 was claimed 3 times and requeued twice before one run finished it

## Goal

The defect the audit office reported as AF-0147 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0147 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0147-bl-1894-was-claimed-3-times-and-requeued-twice-bef.md`.

Location: `logs/DarkFactory-20261009-213949-L1.log:202`

Location: `logs/DarkFactory-20261009-213949-L1.log:202`

Measure-FactoryProcess.ps1 reports BL-1894 claims=3 (rule: 3 or more). Claims: DarkFactory-20261009-162621-L2.log:212 (19:12:18), DarkFactory-20261009-213949-L1.log:187 (22:47:35), DarkFactory-20261010-004419-L1.log:2 (00:44:34). Requeue 1 at L2.log:218 (19:14:03): 'Split into BL-1930, BL-1931 and BL-1932'. The opus run spent 9 turns and $0.55 only to split the task, which planning could have done. Requeue 2 at 213949-L1.log:202 (22:53:20): 'Waits on BL-1944: test1445 ... needs %PWD/%SRCDIR values'. That run spent 33 turns and $1.75 before finding a missing dependency. The third run (20 turns, $0.85) finished it. Waste: $2.30 and about 7 lane-minutes, under the 20%-of-lane-time Medium bar.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261010-012326\logs\DarkFactory-*-L*.log -Pattern 'BL-1894 (claim|REQUEUE)'
```

- Expected: BL-1894 claimed once or twice, never requeued twice
- Actual: tasks[] BL-1894 claims=3; 3 claim lines and 2 REQUEUE lines (19:14:03 split, 22:53:20 waits on BL-1944)

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
