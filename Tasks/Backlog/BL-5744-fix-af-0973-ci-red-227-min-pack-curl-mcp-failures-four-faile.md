---
id: BL-5744
title: Fix AF-0973: CI red 227 min: Pack Curl.Mcp failures, four failed runs with no filed/covered trace line; first green run 16:34 Arizona time
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-22
completed:
---
# BL-5744 — Fix AF-0973: CI red 227 min: Pack Curl.Mcp failures, four failed runs with no filed/covered trace line; first green run 16:34 Arizona time

## Goal

The cause of the factory behaviour the audit office reported as AF-0973 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0973 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0973-ci-red-227-min-pack-curl-mcp-failures-four-failed.md`.

Location: `logs/DarkFactory-20261121-113211.log:45`

Location: `logs/DarkFactory-20261121-113211.log:45`

Red spell 19:47:17Z to 23:34:30Z (227.22 min), started by run 44941565490. That run was filed within 7 minutes as BL-5710, BL-5711 and BL-5712 (log line 34). Runs 44942529813, 44946270671, 44946492351 and 44952616925 ended at 12:59:49, 13:47:30, 14:01:42 and 15:29:04 Arizona time. Their trace lines (log lines 45, 109, 115, 130) carry no filed, covered or passed since. Next green run 44961136795 ended 16:34:30, so more than 30 minutes passed for all four. The shift ended its lanes at 13:29 and then only polled the board until 17:40.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path <log folder>\DarkFactory-20261121-113211.log -Pattern '44942529813|44946270671|44946492351|44952616925'
```

- Expected: Each run has a ci trace line with filed, covered or passed since within 30 minutes of the run ending.
- Actual: Only truncated 'failure on <sha>' lines; no filed or covered. Green only at 16:34:30.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Same cause and fix as BL-5730 (AF-0961 and AF-0973 report the same 227-minute spell); see its Notes. In short: the four runs were traced as covered within minutes, but the 118-character trace cut hid "covered" behind the long `job Pack Curl.Mcp ... step Check the packages` key, the task was never named, and nothing said its covering tasks were interactive only. The CI watch now starts a red run's line with a short head naming the filed or covering tasks, and traces `CI needs you: interactive-only BL-### cover red run <id>` with the tab caption "CI needs you, read" while only interactive-only tasks can fix CI.
- Tests: `-TestCiWatch` 110 cases (11 new or changed) and `-TestLaneMarker`, under Windows PowerShell and PowerShell 7: all pass. `dotnet build`: 0 warnings, 0 errors; fast tests: 115 test projects passed, exit 0.
- Landed in 8119026445 on work/dark-factory (2026-11-22; the Log's 5b3a07c211 is the same commit before its rebase). Criterion 1 needs a factory shift to run on the new code: the task goes back to Backlog (lane: no) until one has, then an interactive session runs the measurement.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-22: Doing -> Backlog. Fix committed (5b3a07c211); criterion 1 needs a factory shift to run on the new code before the measurement, interactive only (lane: no)
