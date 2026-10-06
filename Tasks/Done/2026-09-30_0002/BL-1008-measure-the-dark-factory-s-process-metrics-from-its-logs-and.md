---
id: BL-1008
title: Measure the dark factory's process metrics from its logs and git history
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1000]
touches: [Audit/Tools/Measure-FactoryProcess.ps1, Audit/Tools/Fixtures/process]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1008 — Measure the dark factory's process metrics from its logs and git history

## Goal

`Audit/Tools/Measure-FactoryProcess.ps1 -Since <date>` reads the factory's logs beside the repository, the git history and the CI runs, and writes the process metrics the process auditor (BL-1014) judges: time per task, redone work, time with CI red, idle lane time, overlap-caused waiting and token cost.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1008`.

Sources, as they exist on 2026-09-29 (read `RunDarkFactory.ps1`'s header for the
authoritative description):

- `<repo>.logs\` (e.g. `Z:\repos\Curl.logs`; `-LogRoot` overrides): per-run
  `<ID>-<stamp>[-L<n>][-resolve|-resumed].jsonl` stream-json, whose `result` event
  carries `duration_ms`, `num_turns`, `total_cost_usd` and `usage` token counts;
  `DarkFactory-<stamp>[-L<n>].log` traces with lines like
  `19:10:31 L2 BL-983 claim   <title>` and `19:19:46 L2 -   wait    <why>`; and
  `lanes-<stamp>\lane-<n>.heartbeat.json` files.
- Git: claim commits `chore(tasks): claim BL-### on dark factory lane <n>`, and each
  task file's `## Log` lines (`- yyyy-MM-dd: Doing -> Backlog. <reason>` is a requeue;
  a task claimed more than once is redone work).
- CI: `gh run list --workflow CI --branch work/dark-factory --json
  databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`.

Metrics (BL-1001 lists these exact names as the process auditor's report metrics, so
keep them): `tasksDone`, `medianTaskMinutes`, `p90TaskMinutes` (claim to
Done), `tasksClaimedMoreThanOnce`, `requeues`, `resumedRuns`, `ciRedMinutes` (from the
first failed run after a success to the next success, cancelled runs ignored),
`laneIdleMinutes` (sum of `wait` phases, split into `waitOverlapMinutes` when the reason
says a ready task overlaps one in Doing, and `waitNothingReadyMinutes`), `tokensInput`,
`tokensOutput`, `costUsd`, `costUsdPerTaskDone`, and a per-task table (`id`, `claims`,
`minutes`, `costUsd`, `outcome`).

The script must not depend on the live logs to be tested: `Audit/Tools/Fixtures/process/`
holds a small synthetic log set (two lanes, one task claimed twice, one requeue, a
20-minute overlap wait, one `result` event per run, and a canned `gh run list` JSON) and
`-SelfTest` runs against it with known answers. `gh` is called only when `-CiRunsJson` is
not given.

## Acceptance criteria

- [x] `-SelfTest` prints `PASS` and no `FAIL`, checking every metric above against the fixture's known values.
- [x] Run against the real `Z:\repos\Curl.logs` with `-Since 2026-09-29`, it writes JSON containing every metric name above with a value, and finishes in under two minutes; the command and headline numbers are recorded under Notes.
- [x] It reads logs and git only: `git status --porcelain` and the log folder are unchanged after a run.
- [x] Header help documents sources, metrics (each with its definition) and parameters; ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

- On the audit branch (worktree Z:/repos/Curl.auditbranch), commits dfcbbcfd and 2d42e80c, pull request https://github.com/StewartScottRogers/Curl/pull/36.
- -SelfTest: 17 PASS, 0 FAIL, under Windows PowerShell 5.1 and PowerShell 7.6.6, and from a fresh checkout of the branch.
- Fixture folder is Audit/Tools/Fixtures/process/lane-output, not logs/: the [Ll]ogs/ ignore rule (which stays) left logs/ out of the first commit, and *.log left the traces out, so the two synthetic .log traces are added with git add -f (they are fixtures, not logs).
- Real run: powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-29 -LogRoot Z:\repos\Curl.logs -OutFile <tmp>\process.json - exit 0 in 19 s; git status and the log folder's file list unchanged. Every metric present: tasksDone 232, medianTaskMinutes 19.5, p90TaskMinutes 57.28, tasksClaimedMoreThanOnce 26, requeues 35, resumedRuns 22, ciRedMinutes 913.72 (ciRunsFrom 2026-09-29T11:09:14Z: 500 runs, 375 cancelled), laneIdleMinutes 324 (overlap 0, nothing-ready 0, other 324), tokensInput 1370927951, tokensOutput 8703729, costUsd 695.18, costUsdPerTaskDone 3.00; 244 task rows.
- Fixed during the run: Windows PowerShell 5.1's ConvertFrom-Json emits a JSON array as one object, so @(... | ConvertFrom-Json) made the 500 CI runs one item and ciRedMinutes 0; the output is now parsed into a variable before wrapping.
- Found: all 324 idle minutes are 'other' because 267 lane wait lines logged task-board.ps1's duplicate-ID WARNING (BL-806) instead of the reason; filed BL-1054. Also found and fixed BL-1055 (Measure-Performance.ps1 wrote output to the wrong directory).
- Run from the main checkout the default -LogRoot (<repo>.logs) is right; from the audit worktree it is not, hence -LogRoot above.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Measure-FactoryProcess.ps1 measures the factory's process metrics from its logs and CI, checked against a fixture; in PR #36, awaiting Stewart's merge.
