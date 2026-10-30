---
id: BL-4367
title: Split Curl.Protocol.Ssh.UnitTests so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-30
completed:
---
# BL-4367 — Split Curl.Protocol.Ssh.UnitTests so tasks touching it can run in parallel

## Goal

`Curl.Protocol.Ssh.UnitTests` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 3 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Protocol.Ssh.UnitTests`: BL-4329, BL-4330, BL-4362, BL-4363, BL-4364, BL-4365. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Protocol.Ssh.UnitTests` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-4329, BL-4330, BL-4362, BL-4363, BL-4364, BL-4365) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Protocol.Ssh.UnitTests`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 8c36617f 4b1cb027 a0b49a29.
