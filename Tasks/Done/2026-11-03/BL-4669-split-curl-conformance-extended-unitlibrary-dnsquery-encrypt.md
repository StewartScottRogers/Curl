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
completed: 2026-11-03
---
# BL-4669 — Split Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted so tasks touching it can run in parallel

## Goal

`Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 7 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted`: BL-4474, BL-4514, BL-4515, BL-4516, BL-4666. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4474, BL-4514, BL-4515, BL-4516, BL-4666) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured first: `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted` does not exist yet (nor `DnsQuery/`), so there was no per-unit content to move. BL-4474, the only task that writes there, already says "one file per case group; nothing added to `Harness/`", so each case group it adds touches only its own file. Criterion 1 is met by that: nothing to split, and the rule that keeps it split is in the one task that creates the folder.
- The real cause of the hotspot: BL-4514, BL-4515 and BL-4516 (BL-4473's parts) had no closing `---` on their front matter, so `task-board.ps1` read no fields and took their `touches` as `*`. That made each of them cover every path on the board. Added the closing `---` to all three; a scan of Backlog, Doing, Blocked and Deferred found no other task without it.
- Narrowed BL-4514's `Curl.Conformance.Extended.UnitLibrary/DnsQuery` to `.../DnsQuery/Reference` (the reference server) and BL-4516's to `.../DnsQuery/Transport` (BL-4514's server takes cases without edits), so neither covers `DnsQuery/Encrypted/`. BL-4515 touches no library file. BL-4666 is Done. Recorded under each task's Notes.
- `hotspots` afterwards: `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted` counts 2 (BL-4474 and this task), down from 5; at the default `-Min 5` the board prints `No hotspots.`.
- `dotnet build Curl.slnx` clean; fast tests exit 0 (115 test projects passed). Only task files changed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 862af96a e0f06e4b a0b49a29.
- 2026-11-03: Doing -> Done. BL-4514/4515/4516 front matter closed and touches narrowed; DnsQuery/Encrypted hotspot 5 -> 2, board has no hotspots
