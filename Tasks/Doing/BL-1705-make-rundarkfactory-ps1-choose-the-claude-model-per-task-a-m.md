---
id: BL-1705
title: Make RunDarkFactory.ps1 choose the Claude model per task: a model front-matter field, else a rule on the task, -Model as an override
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, .claude/skills/task-board/SKILL.md, .claude/agents/task-planner.md, CLAUDE.md]
requirement: none
created: 2026-10-07
completed:
---
# BL-1705 — Make RunDarkFactory.ps1 choose the Claude model per task: a model front-matter field, else a rule on the task, -Model as an override

# BL-1705 — Make RunDarkFactory.ps1 choose the Claude model per task: a model front-matter field, else a rule on the task, -Model as an override

## Goal

Each dark factory run uses the Claude model its task calls for - Opus for hard and security-critical work, Sonnet for the rest - instead of one `-Model` (today `opus`) for every lane, and the model each task ran on is visible in the trace, `status.json` and the cost records.

## Context

Decided by Claude under Stewart's delegation; Stewart asked for it on 2026-10-07. Cheaper models for easy tasks stretch the Claude budget that `-Lanes Auto` and `-StopAtUsage` pace against.

Where it lands in `RunDarkFactory.ps1` (line numbers are as of this filing):
- `-Model` parameter (line ~317; default `$env:CLAUDE_MODEL` else `opus`). It is forwarded by the shift start (~3966), the `-Continuous` hand-over (~4245) and so by `-Restart`, and used by `Invoke-TaskRun` (~3114, task runs and the resolver's run) and by the two `--max-turns 1` probes (~1646, ~3507).
- The lane loop (~4357 to 4367) names the task, writes the `claim` trace line (~4362) and the heartbeat (`Write-Heartbeat`, ~500, whose fields feed `status.json` schema 1, ADR-0129), then calls `Invoke-TaskRun`.
- `Get-RecentRunCosts` (~1855) reads `total_cost_usd` from the run logs `BL-###-<stamp>-L<n>[-suffix].jsonl`; `Get-TaskTitle` (~1764) shows how a task file's fields are read.
- Self-tests are the `-Test*` switches (for example `-TestTaskBudget`, `-TestHeartbeat`, `-TestTaskIds`); add `-TestModelChoice` in the same style and list it in the header.

The rule (the task's front matter and board fields are in `.claude/skills/task-board/SKILL.md`):
1. `-Model` other than `auto` (including a `CLAUDE_MODEL` environment value) forces that model for every task: today's behaviour. The default becomes `auto`. Forwarding `-Model` through the hand-over and `-Restart` therefore keeps `auto` as `auto` and a forced value forced.
2. Otherwise a task's own front-matter `model: haiku | sonnet | opus` wins. It is an optional field, like `lane`; a value outside the three is ignored with a trace warning and the rule below applies. A forced `haiku` from `-Model`/`model:` is honoured as written.
3. Otherwise Opus when any of these holds, else Sonnet; Haiku is never chosen by the rule:
   - `touches` names a hand-built security or crypto library folder: `Curl.Cryptography`, `Curl.Tls`, `Curl.Quic`, `Curl.Kerberos`, `Curl.Ntlm`, `Curl.Protocol.Ssh` (the folder or its `.UnitTests` twin);
   - `pipeline` is `feature` or `protocol`;
   - the task is being retried after a failed or stalled run: its `Log` already holds an earlier `Doing` claim that ended in a requeue, a park, or a move back to Backlog (decide the exact test from the Log lines the script writes, and pin it in the self-test);
   - `priority` is High and the title starts with "Fix CI failure" or "Fix flaky CI test" (the tasks the coordinator files from CI, BL-987).
   Sonnet is everything else, which includes `docs`, and `direct` test-only, diagnostic and rename work.
4. The resolver's run and the overtime and resumed runs of a task use that task's model. The two `--max-turns 1` probes use `sonnet` when the mode is `auto`.

Scope boundary: `.claude/skills/task-board/task-board.ps1` is a guard file (an audit path), so this task must not edit it. Nothing here needs it: `model:` is read by `RunDarkFactory.ps1` from the task file, and `task-board.ps1`'s `new` does not need a parameter for it (authors add the line by hand). If a later change to `new` or `status` is wanted, file it as a separate interactive-only task (`lane: no`).

Audit: changing `RunDarkFactory.ps1` makes an audit due (the factory-script cadence in `Audit\Tools\Test-AuditDue.ps1`), but Stewart has paused audits until the accepted findings' tasks are Done. Do not start one for this task.

Overlap: BL-1286, BL-1606, BL-1678, BL-1679, BL-1691 and BL-1692 to BL-1696 also touch `RunDarkFactory.ps1`; the board never runs them at the same time as this one, so no dependency is needed. Rebase carefully if one lands first.

## Acceptance criteria

- [ ] `RunDarkFactory.ps1` has a function that returns the model and the reason for a task (inputs: the forced `-Model` value, the task's front-matter `model`, `pipeline`, `priority`, `touches`, title, and whether it is a retry), and `-Model` defaults to `auto`.
- [ ] A new switch `-TestModelChoice` (documented in the script header beside the other `-Test*` switches) runs table-driven cases and exits non-zero on any failure. It covers at least: a forced `-Model` beating a task's `model:`; `model: haiku`, `sonnet` and `opus` each winning over the rule; an invalid `model:` falling back to the rule; each of the six crypto/security libraries (and a `.UnitTests` twin) giving `opus`; `feature` and `protocol` giving `opus`; a retried task giving `opus`; a High "Fix CI failure ..." task giving `opus`; `docs` and a `direct` diagnostic-output task giving `sonnet`; a Normal ordinary `direct` task giving `sonnet`; no case ever giving `haiku` without being asked.
- [ ] `RunDarkFactory.ps1 -TestModelChoice` and the existing `-TestHeartbeat`, `-TestRestart` and `-TestTaskBudget` switches all pass.
- [ ] `Invoke-TaskRun` passes the chosen model, not the parameter, to `claude --model`; the shift start, the `-Continuous` hand-over and `-Restart` forward `-Model` unchanged, and the self-test for `-Restart` (or a new case in `-TestModelChoice`) shows `auto` surviving a hand-over.
- [ ] The lane's `claim` trace line, and the `end` trace line, state the model and why, for example `claim  <title>  [model sonnet: docs pipeline]`; the lane heartbeat JSON and `status.json` lane objects gain `model` and `modelWhy` fields (schema 1 stays readable: added fields only, and `-TestHeartbeat` shows them).
- [ ] Cost per model can be compared: each task's run logs record the model (in the `end` trace line or a first line of the `.jsonl` log, with `total_cost_usd` still readable by `Get-RecentRunCosts`), and the lane's SUMMARY lines at shift end and the shift's closing summary print the runs, tasks and US dollars per model.
- [ ] The header of `RunDarkFactory.ps1` documents the rule above and the `-Model` override; `.claude/skills/task-board/SKILL.md` lists the optional `model` front-matter field (values, that it wins over the factory's rule, and that Haiku is not chosen by the rule) and its table row notes it is read by `RunDarkFactory.ps1`; `CLAUDE.md`'s "Dark factory" section gains one sentence saying the factory picks the model per task (a task's `model:` wins, `-Model` forces one for all); `.claude/agents/task-planner.md` tells the planner it may set `model:` when a task is clearly easy (`sonnet`) or clearly hard (`opus`) and otherwise leaves it out.
- [ ] `.claude/skills/task-board/task-board.ps1` is unchanged (`git diff --stat` shows it absent), and `Test-AuditPathsUntouched.ps1` would not flag the change.
- [ ] `dotnet build -warnaserror` is clean (the script change must not break the solution) and nothing was started that needs an audit.

## Notes

Not decided here, so do not add scope: a Haiku tier for lanes, per-model cost caps, and choosing the model from task size. File them separately if wanted.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
