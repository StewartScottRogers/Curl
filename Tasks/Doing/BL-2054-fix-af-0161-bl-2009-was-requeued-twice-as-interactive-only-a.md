---
id: BL-2054
title: Fix AF-0161: BL-2009 was requeued twice as interactive only; an interactive commit removed its lane: no between the two lane claims
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-11
completed:
---
# BL-2054 — Fix AF-0161: BL-2009 was requeued twice as interactive only; an interactive commit removed its lane: no between the two lane claims

## Goal

The defect the audit office reported as AF-0161 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0161 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0161-bl-2009-was-requeued-twice-as-interactive-only-an.md`.

Location: `logs/DarkFactory-20261010-213213-L1.log:73`

Location: `logs/DarkFactory-20261010-213213-L1.log:73`

Measure-FactoryProcess row: BL-2009 claims 2, costUsd 2.4277, outcome requeued. Claim 1 at 18:49:00 (DarkFactory-20261010-181711-L1.log:87), 21 turns: 'REQUEUE ... Interactive only: the gap harness and upstream cache for test1642/1643/3036 are refused to lanes ... added lane: no' (line 96). Commit 776f30497 adds '+lane: no'. The interactive commit 4c54032c0 (21:48, 'hand BL-2031 and BL-2009's product-code remainder to lanes') removed it ('-lane: no'). Lane 1 claimed it again at 21:57:24 (DarkFactory-20261010-213213-L1.log:64) and, after 28 turns, requeued it at 22:01:16 for the same reason: 'Interactive only: test3036's retry difference ... appears only in the gap office's cross-check, which lanes may not read' (line 73), re-adding lane: no (84ba607eb). Cause: the hand-back to lanes did not resolve the reason the first lane gave. This also ended the shift with 'nothing ready'.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261011-005116\logs\DarkFactory-*-L*.log -Pattern 'BL-2009 (claim|REQUEUE)'
```

- Expected: At most one lane claim of BL-2009 ends in an 'Interactive only' requeue.
- Actual: Two claims (18:49:00, 21:57:24) and two 'REQUEUE ... Interactive only' lines (18:51:35, 22:01:16).

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: both run prompts in RunDarkFactory.ps1 (single-lane and lane) now tell a lane to write under Notes the step an interactive session must take before a lane can have the task, and to requeue at once, without reproducing, when a reclaimed task shows an earlier "Interactive only" requeue whose reason still holds. The finding's reproduction is a log of past events; the second claim can no longer burn a full run.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
