---
id: BL-5730
title: Fix AF-0961: CI red 227 min (run 44941565490 onward): Pack Curl.Mcp failures left unfixed, four failed runs have no filed/covered trace line
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-21
completed:
---
# BL-5730 — Fix AF-0961: CI red 227 min (run 44941565490 onward): Pack Curl.Mcp failures left unfixed, four failed runs have no filed/covered trace line

## Goal

The cause of the factory behaviour the audit office reported as AF-0961 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0961 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0961-ci-red-227-min-run-44941565490-onward-pack-curl-mc.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells[1]: start 2026-11-21T19:47:17Z, end 23:34:30Z, 227.22 min, runId 44941565490. Failed runs in the spell: 44941565490 (ended 12:47 MST), 44942529813 (12:59), 44943840882 (13:12), 44946270671 (13:47), 44946492351 (14:01), 44952616925 (15:29). Filed within 30 min: 44941565490 (BL-5710/5711/5712 at 12:54:04) and 44943840882 (BL-5713 at 13:18:54). No filed/covered/passed-since line names 44942529813, 44946270671, 44946492351 or 44952616925 in DarkFactory-20261121-113211.log; their lines are truncated at 'failure on <sha>: job Pack Curl.Mcp ...~', and no line in the log copy contains 'covered'. Next green run ended 16:34 MST (44961136795). The three CI fix tasks were requeued interactive-only (12:59:26, 13:00:52, 13:28:53) because the fix is in a guard file or .github. Caveat: the truncated lines may have carried a 'covered' verdict that the log does not show.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-21 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile process.json; Select-String -Path <log folder>\DarkFactory-20261121-113211.log -Pattern '44942529813|44946270671|44946492351|44952616925'
```

- Expected: Each failed run has a filed, covered or passed-since trace line within 30 minutes of its end.
- Actual: Spell of 227.22 min; the four runs have only plain 'failure on' lines with no filed/covered/passed-since wording.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
