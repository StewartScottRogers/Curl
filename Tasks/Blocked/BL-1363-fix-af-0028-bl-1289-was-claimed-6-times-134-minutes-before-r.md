---
id: BL-1363
title: Fix AF-0028: BL-1289 was claimed 6 times (134 minutes) before reaching Done
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed:
---
# BL-1363 — Fix AF-0028: BL-1289 was claimed 6 times (134 minutes) before reaching Done

## Goal

The defect the audit office reported as AF-0028 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0028 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0028-bl-1289-was-claimed-6-times-134-minutes-before-rea.md`.

Location: `logs/BL-1289-20261002-211047-L1.jsonl:1`

Location: `logs/BL-1289-20261002-211047-L1.jsonl:1`

Measure-FactoryProcess.ps1 -Since 2026-09-30 reports BL-1289 with claims=6, minutes=134.62, costUsd=4.19. Run logs exist for lanes L1 and L5. One run ended 'FACTORY: BLOCKED BL-1289 back in Backlog behind BL-1290: the per-chunk allocations still left are in Curl.Console, which BL-1290 holds'. The final run ended 'FACTORY: DONE BL-1289'. The task was queued while its remaining work overlapped BL-1290's touches. That sent it back to Backlog repeatedly. This is a log-based reading only; the 6 claims were not traced one by one. Also in the same period, waitOverlapMinutes equals laneIdleMinutes (3847 of 3847): all lane idle time was overlap waiting.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-30 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object claims -ge 3 | Select-Object id,claims
```

- Expected: No task claimed 3 or more times.
- Actual: BL-1289 claims=6

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

### Run of 2026-10-03 (lane 4): BL-1289 was claimed twice, not six times

Traced claim by claim from `Curl.logs` and git history:

| Evidence | Claims of BL-1289 |
| --- | --- |
| Lane logs: `DarkFactory-20261002-211047-L1.log:163` and `-L5.log:162` (`claim` lines) | 2 |
| Git: `2c3cdecc` (lane 1) and `1bc498a4` (lane 5), the only `claim BL-1289` commits | 2 |
| Task log: `Backlog -> Doing` lines in the BL-1289 file | 2 |
| Run transcripts: one `init` in each of the two `BL-1289-*.jsonl` | 2 runs |

The first run (lane 1, 9.9 minutes) went back to Backlog by design (rule 3: its remaining work was
in `Curl.Console`, held by BL-1290); the second (lane 5, 104.4 minutes) finished it. 134 minutes is
those two runs plus integration. Nothing in `RunDarkFactory.ps1` re-claimed the task.

The number 6 is very likely a counting artefact in the audit office's `Measure-FactoryProcess.ps1`:
the text `Backlog -> Doing` appears 6 times in `BL-1289-20261002-211047-L5.jsonl` (5 times in the
L1 transcript), because each run reads and echoes the task file, whose Log carries that line.

Why this ends Blocked and not Done: the miscount is in an audit-office tool, which a lane may not
read, run or change (ADR-0267; the guard hook refuses it), and the reproduction runs that tool. It
also measures a fixed past window (`-Since 2026-09-30`), so no factory change can alter its output
for BL-1289. Nothing in `touches` (`RunDarkFactory.ps1`) needs to change. What clears it: an
interactive session makes `Measure-FactoryProcess.ps1` count claims from the lane logs' `claim`
lines (or `claim BL-###` commits) instead of transcript text, and AF-0028 is re-read with the
correct count of 2.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Stewart: BL-1289 was claimed 2 times, not 6 (lane logs, git, task log); the miscount is in the audit office's Measure-FactoryProcess.ps1, which only an interactive session may fix - have one count claims from lane-log claim lines and re-read AF-0028?
