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
completed: 2026-11-22
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

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause (found 2026-11-22): the coordinator did trace runs 44942529813, 44946270671, 44946492351 and 44952616925 within minutes of their ends, as `job Pack Curl.Mcp (<os>-latest) step Check the packages regression, covered by a task; ...` (the CI watch found BL-5710 to BL-5712 live and naming each job). But `Get-CiRunOutcome` put each failure's outcome after its key, and `Write-Trace` cuts every line at 118 characters, so the 55-character job key pushed "covered" off the end of the log line (`...step Check the packages~`). "covered by a task" also never named the task, and nothing said the covering tasks were interactive only (their fix is in `.github/workflows/ci.yml`), so the shift idled with CI red and no lane able to fix it.
- Fix (`DarkFactory/CiWatch.ps1`): `Get-CiCoveringTasks` returns the covering tasks by id with whether a lane can take them (`Test-CiTaskInteractiveOnly`: `lane: no`, or the board's guard-file rule); Settled now holds `covered by BL-5710 (interactive only)` / `filed BL-5710 (interactive only)`; a red run's trace line starts with a short head (`failure on d930fa2a: covered by BL-5710, BL-5711, BL-5712 - <failures>`), so the cut never hides it, and an older run in a spell says what its verdict run settled. `Get-CiNeedsYouLine` / `Update-CiNeedsYou` trace `CI needs you: interactive-only BL-### cover red run <id>` and caption the coordinator's tab "CI needs you, read" while a red failure waits only on interactive-only tasks, then trace `CI no longer needs you` and put back the caption it replaced. `DarkFactory/HerdrTabs.ps1`'s `Set-OwnTabLabel` keeps its text in `$script:OwnTabText` for that; `RunDarkFactory.ps1`'s help says so.
- Tests: `-TestCiWatch` 110 cases (11 new or changed: the 2026-11-21 replay, the 118-character cut, covering tasks named and marked, the needs-you line, caption and its restore), and `-TestLaneMarker`, each under Windows PowerShell and PowerShell 7: all pass. `dotnet build`: 0 warnings, 0 errors; fast tests: 115 test projects passed, exit 0.
- Landed in 8119026445 on work/dark-factory (2026-11-22; the Log's 5b3a07c211 is the same commit before its rebase). Criterion 1 needs a factory shift to run on the new code: the task goes back to Backlog (lane: no) until one has, then an interactive session runs the measurement (window from the landing time; expected: every failed run has a `ci` line with filed, covered or passed since within 30 minutes of its end).

- 2026-11-22 (interactive session, measurement): fix `8119026445` landed 2026-11-22 14:11:59 Arizona; window 14:12 to 18:20 Arizona, in which shifts 20261122-144529, -164245 (approx.) and 20261122-174214 ran on the new code. `Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-22T14:11:59 -LogRoot Z:\repos\Curl.logs -CiRunsJson <fresh gh run list, 500 runs>`: ciRedMinutes 0, no red spells; no CI run on work/dark-factory failed in the window, so no run went untraced. That is the finding's expected result over the window. Plainly: because nothing failed, the new covered-by / CI-needs-you lines have not yet fired on a live failure; `-TestCiWatch` (110 cases, incl. a replay of the 2026-11-21 spell) is what exercises them. The finding itself closes only on the process auditor's re-audit (audit started 18:20).

## Log

- 2026-11-21: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-22: Doing -> Backlog. Fix committed (5b3a07c211); criterion 1 needs a factory shift to run on the new code before the measurement, interactive only (lane: no)
- 2026-11-22: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-22: Doing -> Done. CI watch names the covering tasks and says when CI needs you; no untraced red run since the fix landed
