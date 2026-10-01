---
id: BL-1069
title: Fix AF-0003: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-30
completed:
---
# BL-1069 — Fix AF-0003: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs

## Goal

The defect the audit office reported as AF-0003 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0003 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0003-bl-564-was-claimed-3-times-lanes-1-4-and-6-with-tw.md`.

Location: `logs/BL-564-20260929-023709-L4.jsonl`

Location: `logs/BL-564-20260929-023709-L4.jsonl`

The tool reports claims=3 and costUsd=10.33 for BL-564. Lane 4's run ended 'FACTORY: BLOCKED BL-564 back in Backlog: its ADR folder Documentation/Planning/Decisions overlaps BL-883 in Doing'. Lane 6 later finished it ('FACTORY: DONE BL-564') after about 31 minutes. Lane 1 also ran it and finished DONE. Two '-resolve' runs (L1 and L6) then handled ADR-number renumbering conflicts. The shared ADR folder was the touches that serialised and duplicated the work. Overall, 35 tasks were claimed more than once and there were 40 requeues.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-23 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path ..\logs\BL-564-*.jsonl -Pattern 'FACTORY: (DONE|BLOCKED|RESOLVED) BL-564' -List
```

- Expected: Each task claimed once or twice and none requeued twice.
- Actual: BL-564 claims=3 across lanes 1, 4 and 6, with a BLOCKED then DONE sequence and two resolve runs.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
