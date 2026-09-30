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
completed:
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

- [ ] `-SelfTest` prints `PASS` and no `FAIL`, checking every metric above against the fixture's known values.
- [ ] Run against the real `Z:\repos\Curl.logs` with `-Since 2026-09-29`, it writes JSON containing every metric name above with a value, and finishes in under two minutes; the command and headline numbers are recorded under Notes.
- [ ] It reads logs and git only: `git status --porcelain` and the log folder are unchanged after a run.
- [ ] Header help documents sources, metrics (each with its definition) and parameters; ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

## Log

- 2026-09-29: Created.
