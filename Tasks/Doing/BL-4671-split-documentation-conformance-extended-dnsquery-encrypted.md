---
id: BL-4671
title: Split Documentation/Conformance/Extended/DnsQuery-Encrypted.md so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Documentation/Conformance/Extended/DnsQuery-Encrypted.md]
requirement: none
created: 2026-11-03
completed:
---
# BL-4671 — Split Documentation/Conformance/Extended/DnsQuery-Encrypted.md so tasks touching it can run in parallel

## Goal

`Documentation/Conformance/Extended/DnsQuery-Encrypted.md` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 6 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Documentation/Conformance/Extended/DnsQuery-Encrypted.md`: BL-4474, BL-4514, BL-4515, BL-4516, BL-4646. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Documentation/Conformance/Extended/DnsQuery-Encrypted.md` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4474, BL-4514, BL-4515, BL-4516, BL-4646) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Documentation/Conformance/Extended/DnsQuery-Encrypted.md`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-03 (lane 1): There was nothing to split. `Documentation/Conformance/Extended/DnsQuery-Encrypted.md`
  does not exist, either in this checkout or on `origin/work/dark-factory`. BL-4474 creates it, and BL-4474 is the
  only unfinished task whose `touches` names it (checked on the shared branch's Backlog, Doing and Blocked).
  BL-4514, BL-4515 and BL-4516 touch `DnsQuery.md`, a different page. BL-4646 touches only an ADR. So their
  `touches` already name their own files and needed no edit. `task-board.ps1 hotspots` reports "No hotspots", so the
  count for the file is 1, fewer than 5. The coordinator's count of 5 most likely came from an older board, from
  before BL-4473 was split into BL-4514 to BL-4516. The page gets its first content from BL-4474, the only task that
  writes it, so serialising on it costs nothing. Criterion 1 is therefore met trivially: there is no shared content to
  move. `dotnet build` is clean and the fast tests pass (no project failed). No code changed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 18bc5fbd ae3d4f52 a0b49a29.
