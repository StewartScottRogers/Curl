---
id: BL-6050
title: Split Curl.Networking.UnitLibrary/TcpConnector.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6050 — Split Curl.Networking.UnitLibrary/TcpConnector.cs so tasks touching it can run in parallel

## Goal

`Curl.Networking.UnitLibrary/TcpConnector.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 7 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Networking.UnitLibrary/TcpConnector.cs`: BL-6019, BL-6024, BL-6028, BL-6033, BL-6039, BL-6049. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Networking.UnitLibrary/TcpConnector.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-6019, BL-6024, BL-6028, BL-6033, BL-6039, BL-6049) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Networking.UnitLibrary/TcpConnector.cs`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-26: Created.
