---
id: BL-3008
title: Group the shared projects under a Shared solution folder
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.slnx, CLAUDE.md, Documentation/Planning/Decisions/ADR-0731-protocol-projects-sit-in-a-protocols-solution-folder.md]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3008 — Group the shared projects under a Shared solution folder

## Goal

Solution Explorer shows one `Shared` solution folder holding the five shared projects.

## Context

Stewart, 2026-10-21: "Do the same for shared projects", after the Protocols (BL-2963) and
Conformance (BL-3007) solution folders; he chose the name `Shared` and a flat folder.

## Acceptance criteria

- [x] `Curl.slnx` has `/Shared/` holding Audit, Claude, Documentation, Gap and Tasks, and no
      `.shproj` outside it.
- [x] CLAUDE.md and an amendment to ADR-0731 say so.
- [x] `dotnet build Curl.slnx` succeeds and the solution lists the same projects as before.

## Notes

## Log

- 2026-10-21: Created, worked and moved to Done in one interactive session.
