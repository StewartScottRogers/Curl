---
id: BL-4669
title: Split Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted]
requirement: none
created: 2026-11-03
completed:
---
# BL-4669 — Split Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted so tasks touching it can run in parallel

## Goal

`Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 7 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted`: BL-4474, BL-4514, BL-4515, BL-4516, BL-4666. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-4474, BL-4514, BL-4515, BL-4516, BL-4666) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 862af96a e0f06e4b a0b49a29.
