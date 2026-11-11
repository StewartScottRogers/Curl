---
id: BL-4958
title: Run every factory task on sonnet and move to opus only on a retry
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ModelChoice.ps1, RunDarkFactory.ps1, CLAUDE.md, DarkFactory/LaneRules.md, .claude/skills/task-board/SKILL.md]
lane: no
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4958 — Run every factory task on sonnet and move to opus only on a retry

## Goal

Every dark factory task runs on sonnet first, and moves to opus only for a retry after a run came back, so opus is spent only where sonnet had trouble.

## Context

Stewart, 2026-11-11: "lets try and dial back the AI models we are using and only dial them back up temporarily if you are having problems. We are burning through too many tokens". The old rule (BL-1705, BL-3001) put every feature, protocol, crypto-library, retried and High CI-fix task on opus, so nearly every claim of shift 20261111-082724 ran on opus. Rule: `DarkFactory/ModelChoice.ps1`; decision recorded in ADR-1239.

## Acceptance criteria

- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestModelChoice` passes, with cases for a sonnet first run on the feature and protocol pipelines and on a crypto-library task, and opus for a retried task.
- [x] `RunDarkFactory.ps1`'s THE MODEL PER TASK help, the root `CLAUDE.md` and the task-board skill's `model` row describe the new rule, and ADR-1239 records it.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-11: Doing -> Backlog. Criteria were written after the claim (the claim snapshot held the template placeholder); reclaiming so the snapshot matches.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 3e25416a 7d4df1f7 a0b49a29.
- 2026-11-11: Doing -> Done. Every factory first run now uses sonnet and only a retry uses opus; -TestModelChoice 31/31, build clean, 115 fast test projects pass.
