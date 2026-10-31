---
id: BL-4441
title: Group the solution folders as Original and Extended
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.slnx, CLAUDE.md, DarkFactory/LaneRules.md, .claude/skills/new-project/SKILL.md]
requirement: none
lane: no
created: 2026-10-31
completed:
---
# BL-4441 — Group the solution folders as Original and Extended

## Goal

`Curl.slnx` groups curl's own protocols under `/Original/` and everything Curl adds under `/Extended/`: `/Extended/Protocols/<Name>/`, `/Extended/Features/<Name>/` for the option extensions, `/Extended/Abstractions/` and `/Extended/Composition/`; `/Protocols/Abstractions/` stays.

## Context

Stewart, 2026-10-31: rework the solution folders for the renamed projects, option A. Follows BL-4438, BL-4439 and BL-4440 (ADR-1176, ADR-1177). Nothing reads solution folders but `Curl.slnx` itself.

## Acceptance criteria

- [x] `Curl.slnx` has no `/Extensions/` folder: 42 option extensions in `/Extended/Features/<Name>/`, 20 in `/Extended/Protocols/<Name>/`, 16 in `/Original/Protocols/<Name>/`, the plumbing in `/Extended/Abstractions/` and `/Extended/Composition/`.
- [x] The marker comment, `CLAUDE.md`, `LaneRules.md` and the extensions' own `CLAUDE.md` files name the new folders; ADR-1178 amends ADR-0731.
- [x] `dotnet build` of `Curl.slnx` is clean.

## Notes

- 2026-10-31 (interactive): `dotnet build Curl.slnx` clean (0 errors); `dotnet sln list` reads the new tree. No code, test or script reads solution folders, so the fast tests were not rerun for a `Curl.slnx`-only change. Deferred tasks BL-4112 and BL-4131 still name `/Extensions/<Name>/` in their criteria; criteria change only in Backlog, so they are updated when they come back.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 28c871e3 67064269 0b0c3c03.
