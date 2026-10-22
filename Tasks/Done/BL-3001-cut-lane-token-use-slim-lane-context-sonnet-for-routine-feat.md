---
id: BL-3001
title: Cut lane token use: slim lane context, Sonnet for routine features, pre-claim check, exploration limits, turn cap, count killed runs' cost
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskRun.ps1, DarkFactory/LaneRules.md, DarkFactory/ModelChoice.ps1, DarkFactory/Claim.ps1, DarkFactory/Prompts.ps1, DarkFactory/TaskBudget.ps1, RunDarkFactory.ps1, CLAUDE.md]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3001 — Cut lane token use: slim lane context, Sonnet for routine features, pre-claim check, exploration limits, turn cap, count killed runs' cost

## Goal

Lanes do the same work on 25 to 40% fewer tokens, so the Claude Max plan's 5-hour and weekly
limits carry more tasks.

## Context

Stewart, 2026-10-21, asked for ways to cut lane token use and approved "all five plus the accounting
fix". A measurement of all 227 lane runs from 2026-10-19 17:00 to 2026-10-21 (stream-json logs in
`Z:\repos\Curl.logs`) found: 95% of cost is Opus; about 39% is a 55K-token fixed context re-read on
every call (CLAUDE.md, the lane prompt, 42 tools including three MCP servers, about 100 skills, and a
user-level SessionStart hook); 27% is runs that end without Done; runs of 40 or more calls are 19% of
runs and 45% of cost; build and test output is only 1.6%. Killed runs log `total_cost_usd 0`, so the
per-task cost cap undercounts them.

## Acceptance criteria

- [x] Slim lane context: lane runs load no MCP servers, no user-level plugins, skills or hooks that
      the factory does not use, and a short lane rules file (`DarkFactory/LaneRules.md`) in place of
      the full CLAUDE.md, which keeps every rule a lane needs; the first call's cache write is
      measurably smaller than the 37K-token median.
- [x] Routine feature tasks (upstream-case ports, stand-ins and similar, by a rule written down in
      the model-choice code) run on Sonnet; security, crypto, protocol, retried and CI-fix work stays
      on Opus; a task's `model:` front matter still wins.
- [x] Before claiming, a script-only check refuses or flags a task whose `touches` is empty or names
      a whole project it only adds to, or which is too big for one run, so it is fixed before an Opus
      run spends tokens on it.
- [x] The lane prompt limits exploration: capped search output, large files read in parts, and no
      re-reading of a whole file already read.
- [x] A run that reaches a turn cap hands its task back for a split instead of running on.
- [x] A killed or unfinished run's cost is estimated from its logged usage and counts toward the
      task's spend and the recent-run costs.
- [x] Every self-test switch still passes, new behaviour has self-test cases, and an ADR records
      the decisions.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Interactive session; Stewart approved 2026-10-21. Criteria at claim: ee41eb31 cf7c206e 010da289 e39c15bf 9cf48b79 ebe252a9 ed488d39.
- 2026-10-21: Doing -> Done. Lane runs load a slim context (51K -> 29K tokens at the first call), routine features run on Sonnet, an 80-turn cap, a pre-claim check, tighter exploration rules and killed runs priced from usage; ADR-0753; 22 self-tests 374/374.
