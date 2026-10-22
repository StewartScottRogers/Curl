---
id: BL-3010
title: Name the console projects' solution folder Curl
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
# BL-3010 — Name the console projects' solution folder Curl

## Goal

Solution Explorer shows the `Curl.Console` projects under `Hosts/Curl`, named for `curl.exe`.

## Context

Stewart, 2026-10-21, asked to rename Curl.Console and its solution folder to Curl, and chose the
solution folder only ("1"); renaming the projects themselves (about 734 files) was not done.

## Acceptance criteria

- [x] `Curl.slnx` has `/Hosts/Curl/` holding Curl.Console, Curl.Console.IntegrationTests and
      Curl.Console.UnitTests, and no `/Hosts/Console/`.
- [x] CLAUDE.md and an amendment to ADR-0731 say so; `dotnet build Curl.slnx` succeeds.

## Notes

## Log

- 2026-10-21: Created, worked and moved to Done in one interactive session.
