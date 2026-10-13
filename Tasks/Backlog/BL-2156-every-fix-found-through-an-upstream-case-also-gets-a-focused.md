---
id: BL-2156
title: Every fix found through an upstream case also gets a focused unit test in its own library
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md, .claude/skills/task-run, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-13
completed:
---
# BL-2156 — Every fix found through an upstream case also gets a focused unit test in its own library

## Goal

A rule, written where lanes read it, says that any product fix found through an upstream case (the ratchet, a gap finding or an audit conformance finding) also adds a focused unit test in the fixed library's own `.UnitTests` project, so each library's tests stand alone without the upstream cases.

## Context

The third of the three fixes Stewart chose on 2026-10-13 instead of converting the `.rawhttp` cases to hand-written C# tests. Lanes mostly do this already; making it a written rule closes the gaps. Put the rule in CLAUDE.md's solution-wide conventions (or "Build and test commands") and in `.claude/skills/task-run/SKILL.md`'s steps where a lane finishes a fix, and record it in a short ADR marked "Decided by Claude under Stewart's delegation". Do not touch `Audit/` or `Gap/` paths.

## Acceptance criteria

- [ ] CLAUDE.md and the task-run skill state the rule in one or two sentences each, with the same wording.
- [ ] A new ADR records it, cites this task, and is listed in `Documentation/Planning/Decisions/README.md`.
- [ ] `.claude/skills/task-board/templates` or the task template's default criteria for `Close GF-####` and `Fix AF-####` tasks (wherever those are generated outside `Gap/` and `Audit/`) mention the focused unit test, or the Notes say why that file is out of reach for a lane.

## Notes

## Log

- 2026-10-13: Created.
