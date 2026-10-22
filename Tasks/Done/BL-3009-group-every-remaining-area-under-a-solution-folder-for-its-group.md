---
id: BL-3009
title: Group every remaining area under a solution folder for its group
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.slnx, CLAUDE.md, .claude/skills/new-project/SKILL.md, Documentation/Planning/Decisions/ADR-0731-protocol-projects-sit-in-a-protocols-solution-folder.md]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3009 — Group every remaining area under a solution folder for its group

## Goal

Solution Explorer shows a handful of group folders and no flat run of projects.

## Context

Stewart, 2026-10-21: "Can you suggest some other groupings for organising existing projects into
solution folders. There are too many projects to organise myself when looking at a flat
representation of the solution." He accepted Hosts, CommandLine, Transfer, Wire and Security, after
Protocols (BL-2963), Conformance (BL-3007) and Shared (BL-3008).

## Acceptance criteria

- [x] `Curl.slnx` puts each remaining area with its tests in `/<Group>/<Area>/` under Hosts,
      CommandLine, Transfer, Wire or Security, and no project outside a solution folder.
- [x] CLAUDE.md, the `new-project` skill and an amendment to ADR-0731 say so.
- [x] `dotnet build Curl.slnx` succeeds and the solution lists the same projects as before.

## Notes

## Log

- 2026-10-21: Created, worked and moved to Done in one interactive session.
