---
id: AF-0003
title: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs
auditor: process
severity: Medium
status: proposed
key: process:BL-564:BL-564:redone-work
task: none
found: 2026-09-30
found-at: d065d6d3507e2ed87905d40a24233af193913378
scorecard: 2026-09-30_1754.md
closed:
closed-by:
---
# AF-0003 - BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs

## Summary

Medium finding from the process auditor at `logs/BL-564-20260929-023709-L4.jsonl`: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs.

## Evidence

Location: `logs/BL-564-20260929-023709-L4.jsonl`

The tool reports claims=3 and costUsd=10.33 for BL-564. Lane 4's run ended 'FACTORY: BLOCKED BL-564 back in Backlog: its ADR folder Documentation/Planning/Decisions overlaps BL-883 in Doing'. Lane 6 later finished it ('FACTORY: DONE BL-564') after about 31 minutes. Lane 1 also ran it and finished DONE. Two '-resolve' runs (L1 and L6) then handled ADR-number renumbering conflicts. The shared ADR folder was the touches that serialised and duplicated the work. Overall, 35 tasks were claimed more than once and there were 40 requeues.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-23 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path ..\logs\BL-564-*.jsonl -Pattern 'FACTORY: (DONE|BLOCKED|RESOLVED) BL-564' -List
```

- Expected: Each task claimed once or twice and none requeued twice.
- Actual: BL-564 claims=3 across lanes 1, 4 and 6, with a BLOCKED then DONE sequence and two resolve runs.

## Re-audits

