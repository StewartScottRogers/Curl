---
id: BL-5414
title: Split Curl.Original.Protocol.Ldap.UnitTests so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5414 — Split Curl.Original.Protocol.Ldap.UnitTests so tasks touching it can run in parallel

## Goal

`Curl.Original.Protocol.Ldap.UnitTests` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 18 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Original.Protocol.Ldap.UnitTests`: BL-5376, BL-5381, BL-5397, BL-5399, BL-5400. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Original.Protocol.Ldap.UnitTests` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-5376, BL-5381, BL-5397, BL-5399, BL-5400) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Original.Protocol.Ldap.UnitTests`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
